using System.Text.Json.Serialization;

namespace PharmacyPOS.Models.PayMongoApi;

public class MobileCreateCheckoutSessionRequest
{
    [JsonPropertyName("payment_method")]
    public string PaymentMethod { get; set; } = string.Empty;

    [JsonPropertyName("reference_number")]
    public string ReferenceNumber { get; set; } = string.Empty;

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "PHP";

    [JsonPropertyName("customer")]
    public MobileCheckoutCustomerRequest Customer { get; set; } = new();

    [JsonPropertyName("line_items")]
    public List<MobileCheckoutLineItemRequest> LineItems { get; set; } = [];

    [JsonPropertyName("summary")]
    public MobileCheckoutSummaryRequest Summary { get; set; } = new();

    [JsonPropertyName("success_url")]
    public string? SuccessUrl { get; set; }

    [JsonPropertyName("cancel_url")]
    public string? CancelUrl { get; set; }
}
