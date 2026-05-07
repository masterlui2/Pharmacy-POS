using System.ComponentModel.DataAnnotations;
using PharmacyPOS.Models;

namespace PharmacyPOS.Models.Admin;

public enum AdminModuleKind
{
    Pos,
    Payment,
    Receipt,
    SalesHistory,
    Reports,
    StockAlerts,
    Storefront,
    AdminUsers,
    PrescriptionValidation,
    StockLevels,
    Messages,
    AuditLogs
}

public record AdminModuleViewModel
{
    public AdminModuleKind Kind { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Eyebrow { get; init; } = "Admin module";
    public string Description { get; init; } = string.Empty;
    public string ModuleController { get; init; } = "Modules";
    public string CurrentRole { get; init; } = string.Empty;
    public string? Search { get; init; }
    public string? StatusFilter { get; init; }
    public string? PaymentFilter { get; init; }
    public string? CategoryFilter { get; init; }
    public string? RoleFilter { get; init; }
    public string? SelectedMessageSubject { get; init; }
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
    public string? SelectedOrderNumber { get; init; }
    public int? SelectedThreadId { get; init; }
    public bool AllowPaymentUpdates { get; init; }
    public bool AllowReceiptRelease { get; init; } = true;
    public bool AllowRestock { get; init; } = true;
    public AdminPaginationViewModel? Pagination { get; init; }
    public List<AdminMetricCardViewModel> Metrics { get; init; } = [];
    public List<string> Categories { get; init; } = [];
    public List<Medicine> Medicines { get; init; } = [];
    public List<AdminPaymentRowViewModel> Payments { get; init; } = [];
    public List<AdminSalesOrderRowViewModel> Orders { get; init; } = [];
    public List<AdminReceiptSummaryViewModel> ReceiptOrders { get; init; } = [];
    public AdminReceiptViewModel? Receipt { get; init; }
    public List<AdminInventoryAlertViewModel> InventoryAlerts { get; init; } = [];
    public List<AdminTopProductViewModel> TopProducts { get; init; } = [];
    public List<AdminRevenueBreakdownViewModel> RevenueByPaymentMethod { get; init; } = [];
    public List<AdminDailySalesViewModel> DailySales { get; init; } = [];
    public List<AdminUserRowViewModel> Users { get; init; } = [];
    public List<PrescriptionQueueItemViewModel> PrescriptionQueue { get; init; } = [];
    public List<AuditLogEntry> AuditLogs { get; init; } = [];
    public List<PharmacistMessageThreadViewModel> MessageThreads { get; init; } = [];
    public PharmacistMessageThreadViewModel? ActiveMessageThread { get; init; }
    public List<AdminModuleActionViewModel> Actions { get; init; } = [];
    public List<string> WorkflowSteps { get; init; } = [];
    public List<string> FlowSteps { get; init; } = [];
    public List<string> Checklist { get; init; } = [];
}

public class AdminPaginationViewModel
{
    public required string Controller { get; init; }
    public required string Action { get; init; }
    public required int CurrentPage { get; init; }
    public required int PageSize { get; init; }
    public required int TotalItems { get; init; }
    public IReadOnlyDictionary<string, string> RouteValues { get; init; } = new Dictionary<string, string>();
    public IReadOnlyList<int> PageSizeOptions { get; init; } = [10, 25, 50];

    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalItems / (double)PageSize));
    public bool HasPreviousPage => CurrentPage > 1;
    public bool HasNextPage => CurrentPage < TotalPages;
    public int StartItem => TotalItems == 0 ? 0 : ((CurrentPage - 1) * PageSize) + 1;
    public int EndItem => Math.Min(TotalItems, CurrentPage * PageSize);
}

public class AdminMetricCardViewModel
{
    public string Label { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public string Hint { get; init; } = string.Empty;
    public string Icon { get; init; } = "bi-activity";
    public string Tone { get; init; } = "neutral";
}

public class AdminModuleActionViewModel
{
    public string Label { get; init; } = string.Empty;
    public string Controller { get; init; } = "Modules";
    public string Action { get; init; } = string.Empty;
    public string Icon { get; init; } = "bi-arrow-right";
    public string Style { get; init; } = "outline";
    public string Tooltip { get; init; } = string.Empty;
}

public class AdminPaymentRowViewModel
{
    public int PaymentId { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public string PaymentMethod { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string OrderStatus { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string ReferenceNumber { get; init; } = string.Empty;
    public string Provider { get; init; } = string.Empty;
    public bool RequiresPrescription { get; init; }
    public string PrescriptionStatus { get; init; } = string.Empty;
    public bool CanProceed { get; init; } = true;
    public string BlockingReason { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
}

public class AdminSalesOrderRowViewModel
{
    public string OrderNumber { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public string PaymentMethod { get; init; } = string.Empty;
    public string PaymentStatus { get; init; } = string.Empty;
    public string OrderStatus { get; init; } = string.Empty;
    public bool RequiresPrescription { get; init; }
    public string PrescriptionStatus { get; init; } = string.Empty;
    public decimal TotalAmount { get; init; }
    public int ItemsCount { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

public class AdminReceiptSummaryViewModel
{
    public string OrderNumber { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public decimal TotalAmount { get; init; }
    public bool RequiresPrescription { get; init; }
    public string PrescriptionStatus { get; init; } = string.Empty;
    public bool CanRelease { get; init; } = true;
    public string BlockingReason { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
}

public class AdminReceiptViewModel
{
    public string OrderNumber { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public string CustomerPhoneNumber { get; init; } = string.Empty;
    public string PaymentMethod { get; init; } = string.Empty;
    public string PaymentStatus { get; init; } = string.Empty;
    public string ReferenceNumber { get; init; } = string.Empty;
    public string Branch { get; init; } = string.Empty;
    public decimal SubtotalAmount { get; init; }
    public decimal TaxAmount { get; init; }
    public decimal ShippingAmount { get; init; }
    public decimal DiscountAmount { get; init; }
    public decimal TotalAmount { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public List<AdminReceiptItemViewModel> Items { get; init; } = [];
}

public class AdminReceiptItemViewModel
{
    public string ProductName { get; init; } = string.Empty;
    public string BrandName { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal LineTotal => UnitPrice * Quantity;
}

public class AdminInventoryAlertViewModel
{
    public int MedicineId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string BrandName { get; init; } = string.Empty;
    public string GenericName { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Supplier { get; init; } = string.Empty;
    public int Stock { get; init; }
    public DateTime ExpiryDate { get; init; }
    public int DaysUntilExpiry { get; init; }
    public string Status { get; init; } = string.Empty;
    public string Severity { get; init; } = "neutral";
    public string Reason { get; init; } = string.Empty;
}

public class AdminDailySalesViewModel
{
    public string Label { get; init; } = string.Empty;
    public decimal Revenue { get; init; }
    public int Orders { get; init; }
    public double Percentage { get; init; }
}

public class AdminUserRowViewModel
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public int OrdersCount { get; init; }
    public decimal LifetimeSpend { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

public class CounterSaleRequest
{
    [Range(1, int.MaxValue)]
    public int MedicineId { get; init; }

    [Range(1, 9999)]
    public int Quantity { get; init; } = 1;

    [Range(0, 100)]
    public decimal DiscountPercent { get; init; }

    public string PaymentMethod { get; init; } = "Cash";
    public string CustomerName { get; init; } = "Walk-in Customer";
    public string PhoneNumber { get; init; } = "N/A";
    public bool PrescriptionValidated { get; init; }
}

public class PaymentStatusUpdateRequest
{
    [Range(1, int.MaxValue)]
    public int PaymentId { get; init; }

    public string Status { get; init; } = "Paid";
}

public class StockAdjustmentRequest
{
    [Range(1, int.MaxValue)]
    public int MedicineId { get; init; }

    [Range(1, 100000)]
    public int Quantity { get; init; }
}

public class AdminRoleUpdateRequest
{
    [Range(1, int.MaxValue)]
    public int AccountId { get; init; }

    public string Role { get; init; } = "Customer";
}

public class PrescriptionQueueItemViewModel
{
    public string OrderNumber { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public string DeliveryAddress { get; init; } = string.Empty;
    public string PaymentMethod { get; init; } = string.Empty;
    public string PaymentStatus { get; init; } = string.Empty;
    public string PrescriptionStatus { get; init; } = string.Empty;
    public string OrderStatus { get; init; } = string.Empty;
    public int ItemsCount { get; init; }
    public int PrescriptionFileCount { get; init; }
    public bool HasPreviewablePrescription { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

public class PrescriptionStatusUpdateRequest
{
    [Required]
    public string OrderNumber { get; init; } = string.Empty;

    [Required]
    public string Status { get; init; } = "Approved";
}

public class PharmacistMessageThreadViewModel
{
    public int Id { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public string OrderReference { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public string CustomerUid { get; init; } = string.Empty;
    public string OrderStatus { get; init; } = string.Empty;
    public string PaymentStatus { get; init; } = string.Empty;
    public string PrescriptionStatus { get; init; } = string.Empty;
    public bool RequiresPrescription { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
    public int MessageCount { get; init; }
    public bool NeedsReply { get; init; }
    public string LastMessagePreview { get; init; } = string.Empty;
    public List<PharmacistMessageEntryViewModel> Messages { get; init; } = [];
}

public class PharmacistMessageEntryViewModel
{
    public string SenderUid { get; init; } = string.Empty;
    public string SenderName { get; init; } = string.Empty;
    public string SenderRole { get; init; } = string.Empty;
    public string RecipientRole { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public DateTime? SentAtUtc { get; init; }
    public bool IsCustomerMessage => string.Equals(SenderRole, "customer", StringComparison.OrdinalIgnoreCase);
    public bool IsSystemMessage => string.Equals(SenderRole, "system", StringComparison.OrdinalIgnoreCase);
}

public class PharmacistMessageSendRequest
{
    public int? ThreadId { get; init; }

    [Required]
    public string Body { get; init; } = string.Empty;
}

public class PrescriptionPreviewViewModel
{
    public string OrderNumber { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public string PrescriptionStatus { get; init; } = string.Empty;
    public string PaymentStatus { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
    public List<PrescriptionAssetViewModel> Files { get; init; } = [];
}

public class PrescriptionAssetViewModel
{
    public string Url { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public bool CanOpen { get; init; }
    public bool IsImage { get; init; }
    public bool IsPdf { get; init; }
    public string UnavailableMessage { get; init; } = string.Empty;
}

