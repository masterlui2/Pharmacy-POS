namespace PharmacyPOS.Models.Admin;

public class AdminRevenueBreakdownViewModel
{
    public string Label { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public double Percentage { get; init; }
    public int OrdersCount { get; init; }
}
