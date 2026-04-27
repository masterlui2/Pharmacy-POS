using System.Text.Json.Serialization;

namespace PharmacyPOS.Models.PayMongoApi;

public class MobileCreateCheckoutSessionResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("checkout_url")]
    public string CheckoutUrl { get; init; } = string.Empty;

    [JsonPropertyName("order_number")]
    public string OrderNumber { get; init; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;
}
