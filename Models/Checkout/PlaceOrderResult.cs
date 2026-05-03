namespace PharmacyPOS.Models.Checkout;

public class PlaceOrderResult
{
    public bool Success { get; init; }

    public string OrderNumber { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public string FulfillmentBranch { get; init; } = string.Empty;

    public int EstimatedDeliveryMinMinutes { get; init; }

    public int EstimatedDeliveryMaxMinutes { get; init; }

    public decimal TotalAmount { get; init; }

    public string CheckoutUrl { get; init; } = string.Empty;

    public string PaymentStatus { get; init; } = string.Empty;

    public bool AwaitingPrescriptionApproval { get; init; }
}
