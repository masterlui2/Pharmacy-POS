using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PharmacyPOS.Data;
using PharmacyPOS.Models;
using PharmacyPOS.Models.Checkout;

namespace PharmacyPOS.Services;

public class CheckoutService(PharmacyPosDbContext dbContext) : ICheckoutService
{
    private static readonly Dictionary<string, decimal> PromoRates = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SAFEMED10"] = 0.10m,
        ["RXLESS5"] = 0.05m
    };

    public async Task<PlaceOrderResult> PlaceOrderAsync(
        PlaceOrderRequest request,
        string customerEmail,
        CancellationToken cancellationToken = default)
    {
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
        if (requiresPrescription && !string.Equals(request.PrescriptionStatus, "Valid", StringComparison.OrdinalIgnoreCase))
        {
            return new PlaceOrderResult
            {
                Success = false,
                Message = "Prescription medicines require a validated prescription before checkout."
            };
        }

        var shippingProfile = GetShippingProfile(request.DeliveryOption);
        var subtotal = request.Items.Sum(item => item.Price * item.Quantity);
        var taxes = request.Items.Sum(item => item.Tax * item.Quantity);
        var discountRate = GetPromoRate(request.PromoCode);
        var discount = subtotal * discountRate;
        var total = Math.Max(0m, subtotal + taxes + shippingProfile.Fee - discount);

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
            PaymentMethod = request.PaymentMethod.Trim(),
            FulfillmentBranch = shippingProfile.Branch,
            PrescriptionStatus = requiresPrescription ? "Valid" : "NotRequired",
            OrderStatus = "Pending",
            RequiresPrescription = requiresPrescription,
            EstimatedDeliveryMinMinutes = shippingProfile.MinEtaMinutes,
            EstimatedDeliveryMaxMinutes = shippingProfile.MaxEtaMinutes,
            SubtotalAmount = subtotal,
            TaxAmount = taxes,
            ShippingAmount = shippingProfile.Fee,
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

        dbContext.Orders.Add(order);

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

        return new PlaceOrderResult
        {
            Success = true,
            OrderNumber = order.OrderNumber,
            Message = "Order placed successfully.",
            FulfillmentBranch = order.FulfillmentBranch,
            EstimatedDeliveryMinMinutes = order.EstimatedDeliveryMinMinutes,
            EstimatedDeliveryMaxMinutes = order.EstimatedDeliveryMaxMinutes,
            TotalAmount = order.TotalAmount
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

    private static ShippingProfile GetShippingProfile(string deliveryOption) =>
        string.Equals(deliveryOption, "Express", StringComparison.OrdinalIgnoreCase)
            ? new ShippingProfile("Express", 20, 35, 149m, "SafeMed Express Hub - Main Branch")
            : new ShippingProfile("Standard", 45, 75, 79m, "SafeMed Main Branch");

    private static string GenerateOrderNumber() =>
        $"SM-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";

    private static readonly string[] AllowedAddressTypes = ["Home", "Work", "Other"];
    private static readonly string[] AllowedPaymentMethods = ["CashOnDelivery", "EWallet", "Card"];
    private static readonly string[] AllowedDeliveryOptions = ["Standard", "Express"];

    private sealed record ShippingProfile(
        string Code,
        int MinEtaMinutes,
        int MaxEtaMinutes,
        decimal Fee,
        string Branch);
}
