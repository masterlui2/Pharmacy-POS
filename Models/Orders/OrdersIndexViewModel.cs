namespace PharmacyPOS.Models.Orders;

public class OrdersIndexViewModel
{
    public bool ShowSuccessBanner { get; init; }

    public string HighlightOrderNumber { get; init; } = string.Empty;

    public List<OrderSummaryViewModel> Orders { get; init; } = [];

    public int CurrentPage { get; init; } = 1;

    public int PageSize { get; init; } = 5;

    public int TotalOrders { get; init; }

    public int TotalPages => TotalOrders <= 0
        ? 1
        : (int)Math.Ceiling(TotalOrders / (double)PageSize);

    public bool HasPreviousPage => CurrentPage > 1;

    public bool HasNextPage => CurrentPage < TotalPages;

    public int StartItemNumber => TotalOrders == 0
        ? 0
        : ((CurrentPage - 1) * PageSize) + 1;

    public int EndItemNumber => Math.Min(CurrentPage * PageSize, TotalOrders);
}
