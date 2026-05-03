using System.Text.Json;
using System.Text.Json.Serialization;

namespace PharmacyPOS.Models.PayMongoApi;

public class MobileCreateCheckoutSessionRequest
{
    [JsonPropertyName("payment_method")]
    public string PaymentMethod { get; set; } = string.Empty;

    [JsonPropertyName("reference_number")]
    public string ReferenceNumber { get; set; } = string.Empty;

    [JsonPropertyName("customer_uid")]
    public string? CustomerUid { get; set; }

    [JsonPropertyName("customer_email")]
    public string? CustomerEmail { get; set; }

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

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }

    public string? ResolveCustomerUid() =>
        !string.IsNullOrWhiteSpace(CustomerUid)
            ? CustomerUid
            : TryGetAdditionalString("customerUid");

    public string? ResolveCustomerEmail()
    {
        if (!string.IsNullOrWhiteSpace(CustomerEmail))
        {
            return CustomerEmail;
        }

        if (!string.IsNullOrWhiteSpace(Customer.Email))
        {
            return Customer.Email;
        }

        return TryGetAdditionalString("customerEmail");
    }

    private string? TryGetAdditionalString(string key)
    {
        if (AdditionalData is null || !AdditionalData.TryGetValue(key, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }
}
