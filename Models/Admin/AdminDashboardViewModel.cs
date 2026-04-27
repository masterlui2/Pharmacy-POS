namespace PharmacyPOS.Models.Admin;

public class AdminDashboardViewModel
{
    public string Username { get; init; } = "Admin";
    public int TotalCustomers { get; init; }
    public int TotalOrders { get; init; }
    public int PaidOrders { get; init; }
    public int PendingOrders { get; init; }
    public int PrescriptionOrders { get; init; }
    public int TotalMedicines { get; init; }
    public int LowStockMedicines { get; init; }
    public int OutOfStockMedicines { get; init; }
    public decimal TotalRevenue { get; init; }
    public decimal MonthlyRevenue { get; init; }
    public decimal AverageOrderValue { get; init; }
    public List<AdminRevenueBreakdownViewModel> RevenueByPaymentMethod { get; init; } = [];
    public List<AdminTopProductViewModel> TopProducts { get; init; } = [];
    public List<AdminRecentOrderViewModel> RecentOrders { get; init; } = [];
    public AdminPaginationViewModel? RecentOrdersPagination { get; init; }
    public List<string> InventoryAlerts { get; init; } = [];
}
