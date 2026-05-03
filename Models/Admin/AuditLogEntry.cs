namespace PharmacyPOS.Models.Admin;

public sealed class AuditLogEntry
{
    public int Id { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public string Module { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string ActorName { get; set; } = string.Empty;
    public string ActorRole { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
}
