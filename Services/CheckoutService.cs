using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PharmacyPOS.Data;
using PharmacyPOS.Models;
using PharmacyPOS.Models.Checkout;

namespace PharmacyPOS.Services;

public class CheckoutService(
    PharmacyPosDbContext dbContext,
    IPayMongoService payMongoService,
    IOptions<GoogleMapsDeliveryOptions> deliveryOptionsAccessor) : ICheckoutService
{
    private readonly GoogleMapsDeliveryOptions deliveryOptions = deliveryOptionsAccessor.Value;

    private static readonly Dictionary<string, decimal> PromoRates = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SAFEMED10"] = 0.10m,
        ["RXLESS5"] = 0.05m
    };

    public async Task<PlaceOrderResult> PlaceOrderAsync(
        PlaceOrderRequest request,
        string customerEmail,
        string? successReturnUrl,
        string? cancelReturnUrl,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(customerEmail))
        {
            return new PlaceOrderResult
            {
                Success = false,
                Message = "You must sign in before placing an order."
            };
        }

        var validationError = ValidateRequest(request);
        if (!string.IsNullOrWhiteSpace(validationError))
        {
            return new PlaceOrderResult
            {
                Success = false,
                Message = validationError
            };
        }

        var requiresPrescription = request.Items.Any(item => item.RequiresPrescription);
        if (requiresPrescription && request.PrescriptionFiles.Count == 0)
        {
            return new PlaceOrderResult
            {
                Success = false,
                Message = "Prescription upload is required for prescription medicines."
            };
        }

        if (requiresPrescription && !string.Equals(request.PrescriptionStatus, "Valid", StringComparison.OrdinalIgnoreCase))
        {
            return new PlaceOrderResult
            {
                Success = false,
                Message = "Prescription medicines require a validated prescription before checkout."
            };
        }

        var shippingProfile = GetShippingProfile(request.DeliveryOption);
        var deliveryQuote = BuildDeliveryQuote(
            request.DeliveryOption,
            request.Latitude,
            request.Longitude);
        if (!deliveryQuote.IsWithinCoverage)
        {
            return new PlaceOrderResult
            {
                Success = false,
                Message = $"Delivery is currently limited to addresses within {deliveryOptions.MaxRadiusKm:0.#} km of {deliveryOptions.BranchName} in Davao City."
            };
        }

        var subtotal = request.Items.Sum(item => item.Price * item.Quantity);
        var taxes = request.Items.Sum(item => item.Tax * item.Quantity);
        var discountRate = GetPromoRate(request.PromoCode);
        var discount = subtotal * discountRate;
        var paymentMethod = NormalizePaymentMethod(request.PaymentMethod);
        var total = Math.Max(0m, subtotal + taxes + deliveryQuote.TotalFee - discount);

        var account = string.IsNullOrWhiteSpace(customerEmail)
            ? null
            : await dbContext.Accounts.FirstOrDefaultAsync(
                candidate => candidate.Email == customerEmail,
                cancellationToken);

        var order = new PharmacyOrder
        {
            OrderNumber = GenerateOrderNumber(),
            AccountId = account?.Id,
            CustomerFullName = request.FullName.Trim(),
            CustomerEmail = customerEmail,
            CustomerPhoneNumber = request.PhoneNumber.Trim(),
            DeliveryAddress = request.DeliveryAddress.Trim(),
            Landmark = request.Landmark.Trim(),
            AddressType = request.AddressType.Trim(),
            DeliveryOption = shippingProfile.Code,
            PaymentMethod = paymentMethod,
            FulfillmentBranch = deliveryOptions.BranchName,
            PrescriptionStatus = requiresPrescription ? "Valid" : "NotRequired",
            OrderStatus = "Pending",
            RequiresPrescription = requiresPrescription,
            EstimatedDeliveryMinMinutes = shippingProfile.MinEtaMinutes,
            EstimatedDeliveryMaxMinutes = shippingProfile.MaxEtaMinutes,
            SubtotalAmount = subtotal,
            TaxAmount = taxes,
            ShippingAmount = deliveryQuote.TotalFee,
            DiscountAmount = discount,
            TotalAmount = total,
            PromoCode = request.PromoCode.Trim().ToUpperInvariant(),
            PrescriptionFilesJson = JsonSerializer.Serialize(request.PrescriptionFiles),
            CreatedAtUtc = DateTime.UtcNow,
            Items = request.Items.Select(item => new PharmacyOrderItem
            {
                ProductId = item.ProductId.Trim(),
                ProductName = item.Name.Trim(),
                BrandName = item.Brand.Trim(),
                ImageUrl = item.Image.Trim(),
                UnitPrice = item.Price,
                TaxAmount = item.Tax,
                Quantity = item.Quantity,
                RequiresPrescription = item.RequiresPrescription
            }).ToList()
        };

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        dbContext.Orders.Add(order);

        var paymentRecord = new PaymentRecord
        {
            PharmacyOrder = order,
            PaymentMethod = paymentMethod,
            Status = string.Equals(paymentMethod, "CashOnDelivery", StringComparison.OrdinalIgnoreCase)
                ? "PendingCollection"
                : "AwaitingPayment",
            Amount = total,
            ReferenceNumber = GeneratePaymentReference(paymentMethod),
            CreatedAtUtc = DateTime.UtcNow
        };
        dbContext.Payments.Add(paymentRecord);

        if (request.SaveAddress && account is not null)
        {
            dbContext.CustomerAddresses.Add(new CustomerAddress
            {
                AccountId = account.Id,
                FullName = request.FullName.Trim(),
                PhoneNumber = request.PhoneNumber.Trim(),
                DeliveryAddress = request.DeliveryAddress.Trim(),
                Landmark = request.Landmark.Trim(),
                AddressType = request.AddressType.Trim(),
                IsDefault = false,
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        if (!string.Equals(paymentMethod, "CashOnDelivery", StringComparison.OrdinalIgnoreCase))
        {
            var checkoutSession = await payMongoService.CreateCheckoutSessionAsync(
                order,
                order.Items,
                paymentMethod,
                successReturnUrl,
                cancelReturnUrl,
                cancellationToken);

            if (!checkoutSession.Success)
            {
                return new PlaceOrderResult
                {
                    Success = false,
                    Message = checkoutSession.Message
                };
            }

            paymentRecord.Provider = "PayMongo";
            paymentRecord.ProviderCheckoutId = checkoutSession.CheckoutId;
            paymentRecord.CheckoutUrl = checkoutSession.CheckoutUrl;
            paymentRecord.Status = "RedirectedToGateway";
            order.OrderStatus = "AwaitingPayment";
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new PlaceOrderResult
            {
                Success = true,
                OrderNumber = order.OrderNumber,
                Message = checkoutSession.Message,
                FulfillmentBranch = order.FulfillmentBranch,
                EstimatedDeliveryMinMinutes = order.EstimatedDeliveryMinMinutes,
                EstimatedDeliveryMaxMinutes = order.EstimatedDeliveryMaxMinutes,
                TotalAmount = order.TotalAmount,
                CheckoutUrl = checkoutSession.CheckoutUrl,
                PaymentStatus = paymentRecord.Status
            };
        }

        await transaction.CommitAsync(cancellationToken);

        return new PlaceOrderResult
        {
            Success = true,
            OrderNumber = order.OrderNumber,
            Message = "Order placed successfully.",
            FulfillmentBranch = order.FulfillmentBranch,
            EstimatedDeliveryMinMinutes = order.EstimatedDeliveryMinMinutes,
            EstimatedDeliveryMaxMinutes = order.EstimatedDeliveryMaxMinutes,
            TotalAmount = order.TotalAmount,
            PaymentStatus = paymentRecord.Status
        };
    }

    private static string ValidateRequest(PlaceOrderRequest request)
    {
        if (request.Items.Count == 0)
        {
            return "Your cart is empty.";
        }

        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            return "Full name is required.";
        }

        if (string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            return "Phone number is required.";
        }

        if (string.IsNullOrWhiteSpace(request.DeliveryAddress))
        {
            return "Delivery address is required.";
        }

        if (!request.Latitude.HasValue || !request.Longitude.HasValue)
        {
            return "Choose a delivery pin on the map.";
        }

        if (!AllowedAddressTypes.Contains(request.AddressType, StringComparer.OrdinalIgnoreCase))
        {
            return "Select a valid address type.";
        }

        if (!AllowedPaymentMethods.Contains(request.PaymentMethod, StringComparer.OrdinalIgnoreCase))
        {
            return "Select a valid payment method.";
        }

        if (!AllowedDeliveryOptions.Contains(request.DeliveryOption, StringComparer.OrdinalIgnoreCase))
        {
            return "Select a valid delivery option.";
        }

        if (request.Items.Any(item => item.Quantity <= 0 || item.Price < 0 || item.Tax < 0))
        {
            return "One or more cart items are invalid.";
        }

        return string.Empty;
    }

    private static decimal GetPromoRate(string promoCode) =>
        PromoRates.TryGetValue((promoCode ?? string.Empty).Trim(), out var rate)
            ? rate
            : 0m;

    private static string NormalizePaymentMethod(string paymentMethod) =>
        string.Equals(paymentMethod, "EWallet", StringComparison.OrdinalIgnoreCase)
            ? "GCash"
            : paymentMethod.Trim();

    private ShippingProfile GetShippingProfile(string deliveryOption) =>
        string.Equals(deliveryOption, "Express", StringComparison.OrdinalIgnoreCase)
            ? new ShippingProfile("Express", 20, 35, 45m)
            : new ShippingProfile("Standard", 45, 75, 0m);

    private DeliveryQuote BuildDeliveryQuote(
        string deliveryOption,
        double? latitude,
        double? longitude)
    {
        if (!latitude.HasValue || !longitude.HasValue)
        {
            return new DeliveryQuote(false, 0, 0m);
        }

        var distanceKm = CalculateDistanceKm(
            deliveryOptions.BranchLatitude,
            deliveryOptions.BranchLongitude,
            latitude.Value,
            longitude.Value);
        var profile = GetShippingProfile(deliveryOption);
        var billableDistance = Math.Max(0, Math.Ceiling(distanceKm - deliveryOptions.BaseDistanceKm));
        var baseFee = deliveryOptions.BaseFee + (decimal)billableDistance * deliveryOptions.PerKmFee;
        var totalFee = baseFee + profile.SurchargeFee;
        var isWithinCoverage = distanceKm <= deliveryOptions.MaxRadiusKm;

        return new DeliveryQuote(isWithinCoverage, distanceKm, totalFee);
    }

    private static double CalculateDistanceKm(
        double originLatitude,
        double originLongitude,
        double destinationLatitude,
        double destinationLongitude)
    {
        const double earthRadiusKm = 6371;
        var latitudeDelta = DegreesToRadians(destinationLatitude - originLatitude);
        var longitudeDelta = DegreesToRadians(destinationLongitude - originLongitude);
        var startLatitude = DegreesToRadians(originLatitude);
        var endLatitude = DegreesToRadians(destinationLatitude);

        var a =
            Math.Pow(Math.Sin(latitudeDelta / 2), 2) +
            Math.Cos(startLatitude) *
            Math.Cos(endLatitude) *
            Math.Pow(Math.Sin(longitudeDelta / 2), 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return earthRadiusKm * c;
    }

    private static double DegreesToRadians(double degrees) => degrees * (Math.PI / 180);

    private static string GenerateOrderNumber() =>
        $"SM-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";

    private static string GeneratePaymentReference(string paymentMethod)
    {
        var prefix = string.Equals(paymentMethod, "CashOnDelivery", StringComparison.OrdinalIgnoreCase)
            ? "COD"
            : string.Equals(paymentMethod, "GCash", StringComparison.OrdinalIgnoreCase) ||
              string.Equals(paymentMethod, "EWallet", StringComparison.OrdinalIgnoreCase)
                ? "GCS"
                : "CRD";

        return $"{prefix}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(100, 999)}";
    }

    private static readonly string[] AllowedAddressTypes = ["Home", "Work", "Other"];
    private static readonly string[] AllowedPaymentMethods = ["CashOnDelivery", "GCash", "EWallet", "Card"];
    private static readonly string[] AllowedDeliveryOptions = ["Standard", "Express"];

    private sealed record ShippingProfile(
        string Code,
        int MinEtaMinutes,
        int MaxEtaMinutes,
        decimal SurchargeFee);

    private sealed record DeliveryQuote(
        bool IsWithinCoverage,
        double DistanceKm,
        decimal TotalFee);
}
