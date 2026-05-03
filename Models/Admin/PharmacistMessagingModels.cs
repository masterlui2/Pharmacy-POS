namespace PharmacyPOS.Models.Admin;

public sealed class PharmacistMessageThread
{
    public int Id { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string CounterpartyName { get; set; } = string.Empty;
    public string CounterpartyRole { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; }
    public List<PharmacistMessageEntry> Messages { get; set; } = [];
}

public sealed class PharmacistMessageEntry
{
    public int Id { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string SenderRole { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime SentAtUtc { get; set; }
    public bool IsReadByPharmacist { get; set; }
    public bool IsReadByCustomer { get; set; }
}
