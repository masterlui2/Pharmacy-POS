using System.Text.Json;
using PharmacyPOS.Models.Admin;

namespace PharmacyPOS.Services;

public sealed class FileAuditLogService(
    IWebHostEnvironment environment,
    ILogger<FileAuditLogService> logger) : IAuditLogService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly string filePath = Path.Combine(environment.ContentRootPath, "App_Data", "audit-logs.json");

    public async Task WriteAsync(
        string module,
        string action,
        string actorName,
        string actorRole,
        string details,
        CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var logs = await ReadAllInternalAsync(cancellationToken);
            var nextId = logs.Count == 0 ? 1 : logs.Max(entry => entry.Id) + 1;
            logs.Add(new AuditLogEntry
            {
                Id = nextId,
                OccurredAtUtc = DateTime.UtcNow,
                Module = module.Trim(),
                Action = action.Trim(),
                ActorName = actorName.Trim(),
                ActorRole = actorRole.Trim(),
                Details = details.Trim()
            });

            await WriteAllInternalAsync(logs, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to write an audit log entry.");
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<IReadOnlyList<AuditLogEntry>> GetRecentAsync(
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var logs = await ReadAllInternalAsync(cancellationToken);
            return logs
                .OrderByDescending(entry => entry.OccurredAtUtc)
                .Take(Math.Max(1, limit))
                .ToList();
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<List<AuditLogEntry>> ReadAllInternalAsync(CancellationToken cancellationToken)
    {
        EnsureStorage();
        var json = await File.ReadAllTextAsync(filePath, cancellationToken);
        return JsonSerializer.Deserialize<List<AuditLogEntry>>(json, JsonOptions) ?? [];
    }

    private async Task WriteAllInternalAsync(List<AuditLogEntry> logs, CancellationToken cancellationToken)
    {
        EnsureStorage();
        var json = JsonSerializer.Serialize(logs, JsonOptions);
        await File.WriteAllTextAsync(filePath, json, cancellationToken);
    }

    private void EnsureStorage()
    {
        var directory = Path.GetDirectoryName(filePath)!;
        Directory.CreateDirectory(directory);
        if (!File.Exists(filePath))
        {
            File.WriteAllText(filePath, "[]");
        }
    }
}
