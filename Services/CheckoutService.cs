using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PharmacyPOS.Data;
using PharmacyPOS.Models;
using PharmacyPOS.Models.Checkout;
using PharmacyPOS.Models.PayMongoApi;

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

        return await PersistOrderAsync(
            new OrderCreationRequest
            {
                CustomerEmail = customerEmail,
                PaymentReference = null,
                FullName = request.FullName,
                PhoneNumber = request.PhoneNumber,
                DeliveryAddress = request.DeliveryAddress,
                Landmark = request.Landmark,
                AddressType = request.AddressType,
                DeliveryOption = shippingProfile.Code,
                PaymentMethod = paymentMethod,
                PrescriptionStatus = requiresPrescription ? "Valid" : "NotRequired",
                RequiresPrescription = requiresPrescription,
                EstimatedDeliveryMinMinutes = shippingProfile.MinEtaMinutes,
                EstimatedDeliveryMaxMinutes = shippingProfile.MaxEtaMinutes,
                Subtotal = subtotal,
                Taxes = taxes,
                ShippingAmount = deliveryQuote.TotalFee,
                DiscountAmount = discount,
                TotalAmount = total,
                PromoCode = request.PromoCode,
                PrescriptionFiles = request.PrescriptionFiles,
                SaveAddress = request.SaveAddress,
                Items = request.Items.Select(item => new OrderItemCreationRequest
                {
                    ProductId = item.ProductId,
                    Name = item.Name,
                    Brand = item.Brand,
                    Image = item.Image,
                    UnitPrice = item.Price,
                    TaxAmount = item.Tax,
                    Quantity = item.Quantity,
                    RequiresPrescription = item.RequiresPrescription
                }).ToList()
            },
            account,
            successReturnUrl,
            cancelReturnUrl,
            cancellationToken);
    }

    public async Task<PlaceOrderResult> CreateMobileCheckoutSessionAsync(
        MobileCreateCheckoutSessionRequest request,
        string? successReturnUrl,
        string? cancelReturnUrl,
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidateMobileRequest(request);
        if (!string.IsNullOrWhiteSpace(validationError))
        {
            return new PlaceOrderResult
            {
                Success = false,
                Message = validationError
            };
        }

        var paymentMethod = NormalizePaymentMethod(request.PaymentMethod);
        var subtotal = FromMinorAmount(request.Summary.Subtotal);
        var deliveryFee = FromMinorAmount(request.Summary.DeliveryFee);
        var serviceFee = FromMinorAmount(request.Summary.ServiceFee);
        var total = FromMinorAmount(request.Summary.Total);
        var shippingAmount = deliveryFee + serviceFee;
        var customerEmail = request.Customer.Email?.Trim() ?? string.Empty;
        var deliveryAddress = BuildMobileDeliveryAddress(request.Customer);

        var account = string.IsNullOrWhiteSpace(customerEmail)
            ? null
            : await dbContext.Accounts.FirstOrDefaultAsync(
                candidate => candidate.Email == customerEmail,
                cancellationToken);

        return await PersistOrderAsync(
            new OrderCreationRequest
            {
                CustomerEmail = customerEmail,
                PaymentReference = string.IsNullOrWhiteSpace(request.ReferenceNumber)
                    ? null
                    : request.ReferenceNumber.Trim(),
                FullName = request.Customer.Name,
                PhoneNumber = request.Customer.Phone,
                DeliveryAddress = deliveryAddress,
                Landmark = request.Customer.Notes,
                AddressType = NormalizeAddressType(request.Customer.AddressLabel),
                DeliveryOption = "Standard",
                PaymentMethod = paymentMethod,
                PrescriptionStatus = "NotRequired",
                RequiresPrescription = false,
                EstimatedDeliveryMinMinutes = 45,
                EstimatedDeliveryMaxMinutes = 75,
                Subtotal = subtotal,
                Taxes = 0m,
                ShippingAmount = shippingAmount,
                DiscountAmount = 0m,
                TotalAmount = total,
                PromoCode = string.Empty,
                PrescriptionFiles = [],
                SaveAddress = false,
                Items = request.LineItems.Select((item, index) => new OrderItemCreationRequest
                {
                    ProductId = BuildMobileProductId(request.ReferenceNumber, index),
                    Name = item.Name,
                    Brand = item.Description ?? string.Empty,
                    Image = string.Empty,
                    UnitPrice = FromMinorAmount(item.Amount),
                    TaxAmount = 0m,
                    Quantity = item.Quantity,
                    RequiresPrescription = false
                }).ToList()
            },
            account,
            successReturnUrl,
            cancelReturnUrl,
            cancellationToken);
    }

    private async Task<PlaceOrderResult> PersistOrderAsync(
        OrderCreationRequest request,
        Account? account,
        string? successReturnUrl,
        string? cancelReturnUrl,
        CancellationToken cancellationToken)
    {
        var order = new PharmacyOrder
        {
            OrderNumber = GenerateOrderNumber(),
            AccountId = account?.Id,
            CustomerFullName = request.FullName.Trim(),
            CustomerEmail = request.CustomerEmail.Trim(),
            CustomerPhoneNumber = request.PhoneNumber.Trim(),
            DeliveryAddress = request.DeliveryAddress.Trim(),
            Landmark = request.Landmark.Trim(),
            AddressType = request.AddressType.Trim(),
            DeliveryOption = request.DeliveryOption.Trim(),
            PaymentMethod = request.PaymentMethod,
            FulfillmentBranch = deliveryOptions.BranchName,
            PrescriptionStatus = request.PrescriptionStatus,
            OrderStatus = "Pending",
            RequiresPrescription = request.RequiresPrescription,
            EstimatedDeliveryMinMinutes = request.EstimatedDeliveryMinMinutes,
            EstimatedDeliveryMaxMinutes = request.EstimatedDeliveryMaxMinutes,
            SubtotalAmount = request.Subtotal,
            TaxAmount = request.Taxes,
            ShippingAmount = request.ShippingAmount,
            DiscountAmount = request.DiscountAmount,
            TotalAmount = request.TotalAmount,
            PromoCode = request.PromoCode.Trim().ToUpperInvariant(),
            PrescriptionFilesJson = JsonSerializer.Serialize(request.PrescriptionFiles),
            CreatedAtUtc = DateTime.UtcNow,
            Items = request.Items.Select(item => new PharmacyOrderItem
            {
                ProductId = item.ProductId.Trim(),
                ProductName = item.Name.Trim(),
                BrandName = item.Brand.Trim(),
                ImageUrl = item.Image.Trim(),
                UnitPrice = item.UnitPrice,
                TaxAmount = item.TaxAmount,
                Quantity = item.Quantity,
                RequiresPrescription = item.RequiresPrescription
            }).ToList()
        };

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        dbContext.Orders.Add(order);

        var paymentRecord = new PaymentRecord
        {
            PharmacyOrder = order,
            PaymentMethod = request.PaymentMethod,
            Status = string.Equals(request.PaymentMethod, "CashOnDelivery", StringComparison.OrdinalIgnoreCase)
                ? "PendingCollection"
                : "AwaitingPayment",
            Amount = request.TotalAmount,
            ReferenceNumber = request.PaymentReference ?? GeneratePaymentReference(request.PaymentMethod),
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

        if (!string.Equals(request.PaymentMethod, "CashOnDelivery", StringComparison.OrdinalIgnoreCase))
        {
            var checkoutSession = await payMongoService.CreateCheckoutSessionAsync(
                order,
                order.Items,
                request.PaymentMethod,
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

    private static string ValidateMobileRequest(MobileCreateCheckoutSessionRequest request)
    {
        if (request.Customer is null)
        {
            return "Customer details are required.";
        }

        if (request.LineItems.Count == 0)
        {
            return "At least one line item is required.";
        }

        if (request.Summary is null)
        {
            return "Order summary is required.";
        }

        if (string.IsNullOrWhiteSpace(request.Customer.Name))
        {
            return "Customer name is required.";
        }

        if (string.IsNullOrWhiteSpace(request.ReferenceNumber))
        {
            return "Reference number is required.";
        }

        if (string.IsNullOrWhiteSpace(request.Customer.Phone))
        {
            return "Customer phone is required.";
        }

        if (string.IsNullOrWhiteSpace(request.Customer.StreetAddress))
        {
            return "Street address is required.";
        }

        if (string.IsNullOrWhiteSpace(request.Customer.City))
        {
            return "City is required.";
        }

        if (!string.Equals(request.Currency, "PHP", StringComparison.OrdinalIgnoreCase))
        {
            return "Only PHP currency is supported.";
        }

        if (!AllowedMobilePaymentMethods.Contains(request.PaymentMethod, StringComparer.OrdinalIgnoreCase))
        {
            return "Unsupported payment method. Use gcash, paymaya, or card.";
        }

        if (request.LineItems.Any(item =>
                string.IsNullOrWhiteSpace(item.Name) ||
                item.Quantity <= 0 ||
                item.Amount <= 0 ||
                !string.Equals(item.Currency, "PHP", StringComparison.OrdinalIgnoreCase)))
        {
            return "One or more line items are invalid.";
        }

        if (request.Summary.Subtotal < 0 || request.Summary.DeliveryFee < 0 || request.Summary.ServiceFee < 0 || request.Summary.Total <= 0)
        {
            return "Order summary amounts must be valid positive values.";
        }

        var lineItemsTotal = request.LineItems.Sum(item => item.Amount * item.Quantity);
        if (lineItemsTotal != request.Summary.Subtotal)
        {
            return "Summary subtotal does not match the submitted line items.";
        }

        if (request.Summary.Subtotal + request.Summary.DeliveryFee + request.Summary.ServiceFee != request.Summary.Total)
        {
            return "Summary total does not match subtotal, delivery fee, and service fee.";
        }

        return string.Empty;
    }

    private static decimal GetPromoRate(string promoCode) =>
        PromoRates.TryGetValue((promoCode ?? string.Empty).Trim(), out var rate)
            ? rate
            : 0m;

    private static string NormalizePaymentMethod(string paymentMethod) =>
        paymentMethod.Trim().ToLowerInvariant() switch
        {
            "ewallet" => "GCash",
            "gcash" => "GCash",
            "paymaya" => "PayMaya",
            "maya" => "PayMaya",
            "card" => "Card",
            "cashondelivery" => "CashOnDelivery",
            _ => paymentMethod.Trim()
        };

    private static string NormalizeAddressType(string? addressLabel)
    {
        if (AllowedAddressTypes.Contains(addressLabel ?? string.Empty, StringComparer.OrdinalIgnoreCase))
        {
            return addressLabel!.Trim();
        }

        return "Home";
    }

    private static string BuildMobileDeliveryAddress(MobileCheckoutCustomerRequest customer)
    {
        var parts = new[]
        {
            customer.StreetAddress?.Trim(),
            customer.Barangay?.Trim(),
            customer.City?.Trim()
        };

        return string.Join(", ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private static string BuildMobileProductId(string? referenceNumber, int index)
    {
        var prefix = string.IsNullOrWhiteSpace(referenceNumber)
            ? "mobile"
            : referenceNumber.Trim();

        return $"{prefix}-{index + 1}";
    }

    private static decimal FromMinorAmount(long amount) => amount / 100m;

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
    private static readonly string[] AllowedPaymentMethods = ["CashOnDelivery", "GCash", "EWallet", "Card", "PayMaya", "Maya"];
    private static readonly string[] AllowedDeliveryOptions = ["Standard", "Express"];
    private static readonly string[] AllowedMobilePaymentMethods = ["gcash", "paymaya", "maya", "card"];

    private sealed record ShippingProfile(
        string Code,
        int MinEtaMinutes,
        int MaxEtaMinutes,
        decimal SurchargeFee);

    private sealed record DeliveryQuote(
        bool IsWithinCoverage,
        double DistanceKm,
        decimal TotalFee);

    private sealed record OrderCreationRequest
    {
        public string CustomerEmail { get; init; } = string.Empty;
        public string? PaymentReference { get; init; }
        public string FullName { get; init; } = string.Empty;
        public string PhoneNumber { get; init; } = string.Empty;
        public string DeliveryAddress { get; init; } = string.Empty;
        public string Landmark { get; init; } = string.Empty;
        public string AddressType { get; init; } = "Home";
        public bool SaveAddress { get; init; }
        public string DeliveryOption { get; init; } = "Standard";
        public string PaymentMethod { get; init; } = "CashOnDelivery";
        public string PrescriptionStatus { get; init; } = "NotRequired";
        public bool RequiresPrescription { get; init; }
        public int EstimatedDeliveryMinMinutes { get; init; }
        public int EstimatedDeliveryMaxMinutes { get; init; }
        public decimal Subtotal { get; init; }
        public decimal Taxes { get; init; }
        public decimal ShippingAmount { get; init; }
        public decimal DiscountAmount { get; init; }
        public decimal TotalAmount { get; init; }
        public string PromoCode { get; init; } = string.Empty;
        public List<string> PrescriptionFiles { get; init; } = [];
        public List<OrderItemCreationRequest> Items { get; init; } = [];
    }

    private sealed record OrderItemCreationRequest
    {
        public string ProductId { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Brand { get; init; } = string.Empty;
        public string Image { get; init; } = string.Empty;
        public decimal UnitPrice { get; init; }
        public decimal TaxAmount { get; init; }
        public int Quantity { get; init; }
        public bool RequiresPrescription { get; init; }
    }
}
