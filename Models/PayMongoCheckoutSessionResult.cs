namespace PharmacyPOS.Models;

public class PayMongoCheckoutSessionResult
{
    public bool Success { get; init; }

    public string CheckoutId { get; init; } = string.Empty;

    public string CheckoutUrl { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;
}
