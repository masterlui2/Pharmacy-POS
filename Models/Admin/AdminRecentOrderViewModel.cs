namespace PharmacyPOS.Models.Admin;

public class AdminRecentOrderViewModel
{
    public string OrderNumber { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public string OrderStatus { get; init; } = string.Empty;
    public string PaymentStatus { get; init; } = string.Empty;
    public string PerformedByName { get; init; } = string.Empty;
    public string PerformedByRole { get; init; } = string.Empty;
    public decimal TotalAmount { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}
