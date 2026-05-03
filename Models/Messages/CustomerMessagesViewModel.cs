using System.ComponentModel.DataAnnotations;

namespace PharmacyPOS.Models.Messages;

public sealed class CustomerMessagesViewModel
{
    public string CustomerName { get; init; } = string.Empty;
    public string SupportName { get; init; } = "SafeMed Pharmacist";
    public string SupportSubtitle { get; init; } = "Prescription review, order updates, and pharmacist guidance.";
    public string Subject { get; init; } = "Pharmacist Support";
    public int UnreadCount { get; init; }
    public bool HasThread { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
    public List<CustomerMessageEntryViewModel> Messages { get; init; } = [];
}

public sealed class CustomerMessageEntryViewModel
{
    public string SenderName { get; init; } = string.Empty;
    public string SenderRole { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public DateTime SentAtUtc { get; init; }
    public bool IsCustomerMessage { get; init; }
}

public sealed class CustomerMessageSendRequest
{
    public string Subject { get; init; } = "Pharmacist Support";

    [Required]
    public string Body { get; init; } = string.Empty;
}
