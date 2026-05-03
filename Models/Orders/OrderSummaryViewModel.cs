namespace PharmacyPOS.Models.Orders;

public class OrderSummaryViewModel
{
    public string OrderNumber { get; init; } = string.Empty;

    public string OrderStatus { get; init; } = string.Empty;

    public string PaymentStatus { get; init; } = string.Empty;

    public string PaymentMethod { get; init; } = string.Empty;

    public string FulfillmentBranch { get; init; } = string.Empty;

    public string DeliveryOption { get; init; } = string.Empty;

    public bool RequiresPrescription { get; init; }

    public string PrescriptionStatus { get; init; } = string.Empty;

    public DateTime CreatedAtUtc { get; init; }

    public decimal TotalAmount { get; init; }

    public bool CanContinuePayment { get; init; }

    public bool ShowContinuePayment { get; init; }

    public string ContinuePaymentLabel { get; init; } = string.Empty;

    public List<OrderItemViewModel> Items { get; init; } = [];
}
