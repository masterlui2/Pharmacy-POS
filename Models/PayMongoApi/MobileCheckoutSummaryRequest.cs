using System.Text.Json.Serialization;

namespace PharmacyPOS.Models.PayMongoApi;

public class MobileCheckoutSummaryRequest
{
    [JsonPropertyName("subtotal")]
    public long Subtotal { get; set; }

    [JsonPropertyName("delivery_fee")]
    public long DeliveryFee { get; set; }

    [JsonPropertyName("service_fee")]
    public long ServiceFee { get; set; }

    [JsonPropertyName("total")]
    public long Total { get; set; }
}
