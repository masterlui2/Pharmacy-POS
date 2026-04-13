namespace PharmacyPOS.Models.Orders;

public class OrdersIndexViewModel
{
    public bool ShowSuccessBanner { get; init; }

    public string HighlightOrderNumber { get; init; } = string.Empty;

    public List<OrderSummaryViewModel> Orders { get; init; } = [];
}
