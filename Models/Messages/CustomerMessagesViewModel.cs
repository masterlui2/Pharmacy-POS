using System.ComponentModel.DataAnnotations;

namespace PharmacyPOS.Models.Messages;

public sealed class CustomerMessagesViewModel
{
    public string CustomerName { get; init; } = string.Empty;
    public string SupportName { get; init; } = "SafeMed Pharmacist";
    public string SupportSubtitle { get; init; } = "Prescription review, order updates, and pharmacist guidance.";
    public int UnreadCount { get; init; }
    public string SelectedOrderNumber { get; init; } = string.Empty;
    public List<CustomerOrderThreadViewModel> OrderThreads { get; init; } = [];
    public CustomerOrderThreadViewModel? ActiveThread { get; init; }
    public bool HasOrders => OrderThreads.Count > 0;
}

public sealed class CustomerMessageEntryViewModel
{
    public string SenderName { get; init; } = string.Empty;
    public string SenderRole { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public DateTime SentAtUtc { get; init; }
    public bool IsCustomerMessage { get; init; }
}

public sealed class CustomerOrderThreadViewModel
{
    public int? ThreadId { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public string OrderReference { get; init; } = string.Empty;
    public string OrderStatus { get; init; } = string.Empty;
    public string PaymentStatus { get; init; } = string.Empty;
    public string PrescriptionStatus { get; init; } = string.Empty;
    public bool RequiresPrescription { get; init; }
    public decimal TotalAmount { get; init; }
    public int ItemsCount { get; init; }
    public string ItemSummary { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
    public int UnreadCount { get; init; }
    public string LastMessagePreview { get; init; } = string.Empty;
    public List<CustomerOrderItemViewModel> Items { get; init; } = [];
    public List<CustomerMessageEntryViewModel> Messages { get; init; } = [];
}

public sealed class CustomerOrderItemViewModel
{
    public string ProductName { get; init; } = string.Empty;
    public string BrandName { get; init; } = string.Empty;
    public int Quantity { get; init; }
}

public sealed class CustomerMessageSendRequest
{
    [Required]
    public string OrderNumber { get; init; } = string.Empty;

    [Required]
    public string Body { get; init; } = string.Empty;
}
