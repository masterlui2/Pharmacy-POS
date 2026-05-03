using PharmacyPOS.Models.Admin;

namespace PharmacyPOS.Services;

public interface IAuditLogService
{
    Task WriteAsync(
        string module,
        string action,
        string actorName,
        string actorRole,
        string details,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuditLogEntry>> GetRecentAsync(
        int limit = 100,
        CancellationToken cancellationToken = default);
}
