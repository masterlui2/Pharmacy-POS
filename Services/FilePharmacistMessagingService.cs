using System.Text.Json;
using PharmacyPOS.Models.Admin;
using PharmacyPOS.Models.Security;

namespace PharmacyPOS.Services;

public sealed class FilePharmacistMessagingService(
    IWebHostEnvironment environment,
    ILogger<FilePharmacistMessagingService> logger) : IPharmacistMessagingService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly string filePath = Path.Combine(environment.ContentRootPath, "App_Data", "pharmacist-messages.json");

    public async Task<IReadOnlyList<PharmacistMessageThread>> GetThreadsAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var threads = await ReadAllInternalAsync(cancellationToken);
            return threads
                .OrderByDescending(thread => thread.UpdatedAtUtc)
                .ToList();
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<PharmacistMessageThread?> GetThreadAsync(int threadId, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var threads = await ReadAllInternalAsync(cancellationToken);
            return threads.FirstOrDefault(thread => thread.Id == threadId);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<PharmacistMessageThread?> GetCustomerThreadAsync(string customerEmail, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(customerEmail))
        {
            return null;
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            var threads = await ReadAllInternalAsync(cancellationToken);
            return threads
                .Where(thread => string.Equals(thread.CustomerEmail, customerEmail.Trim(), StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(thread => thread.UpdatedAtUtc)
                .FirstOrDefault();
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<int> SendMessageAsync(
        int? threadId,
        string subject,
        string senderName,
        string senderRole,
        string body,
        CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var threads = await ReadAllInternalAsync(cancellationToken);
            PharmacistMessageThread thread;
            if (threadId.HasValue)
            {
                thread = threads.FirstOrDefault(candidate => candidate.Id == threadId.Value)
                    ?? throw new InvalidOperationException("Message thread was not found.");
            }
            else
            {
                var nextThreadId = threads.Count == 0 ? 1 : threads.Max(candidate => candidate.Id) + 1;
                thread = new PharmacistMessageThread
                {
                    Id = nextThreadId,
                    Subject = subject.Trim(),
                    CounterpartyName = "Admin Operations",
                    CounterpartyRole = AppRoles.Admin,
                    UpdatedAtUtc = DateTime.UtcNow
                };
                threads.Add(thread);
            }

            var nextMessageId = thread.Messages.Count == 0 ? 1 : thread.Messages.Max(entry => entry.Id) + 1;
            thread.Messages.Add(new PharmacistMessageEntry
            {
                Id = nextMessageId,
                SenderName = senderName.Trim(),
                SenderRole = senderRole.Trim(),
                Body = body.Trim(),
                SentAtUtc = DateTime.UtcNow,
                IsReadByPharmacist = AppRoles.Matches(senderRole, AppRoles.Pharmacist),
                IsReadByCustomer = !HasCustomerParticipant(thread) || AppRoles.Matches(senderRole, AppRoles.Customer)
            });
            thread.UpdatedAtUtc = DateTime.UtcNow;

            await WriteAllInternalAsync(threads, cancellationToken);
            return thread.Id;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to send pharmacist message.");
            throw;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<int> SendCustomerMessageAsync(
        string customerName,
        string customerEmail,
        string customerPhone,
        string subject,
        string body,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(customerEmail))
        {
            throw new InvalidOperationException("Customer email is required to send a support message.");
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            var threads = await ReadAllInternalAsync(cancellationToken);
            var normalizedEmail = customerEmail.Trim();
            var thread = threads
                .Where(candidate => string.Equals(candidate.CustomerEmail, normalizedEmail, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(candidate => candidate.UpdatedAtUtc)
                .FirstOrDefault();

            if (thread is null)
            {
                var nextThreadId = threads.Count == 0 ? 1 : threads.Max(candidate => candidate.Id) + 1;
                thread = new PharmacistMessageThread
                {
                    Id = nextThreadId,
                    Subject = string.IsNullOrWhiteSpace(subject) ? "Pharmacist Support" : subject.Trim(),
                    CounterpartyName = "SafeMed Pharmacist",
                    CounterpartyRole = AppRoles.Pharmacist,
                    CustomerName = customerName.Trim(),
                    CustomerEmail = normalizedEmail,
                    CustomerPhone = customerPhone.Trim(),
                    UpdatedAtUtc = DateTime.UtcNow
                };
                threads.Add(thread);
            }
            else
            {
                thread.CustomerName = string.IsNullOrWhiteSpace(customerName) ? thread.CustomerName : customerName.Trim();
                thread.CustomerPhone = string.IsNullOrWhiteSpace(customerPhone) ? thread.CustomerPhone : customerPhone.Trim();
                if (string.IsNullOrWhiteSpace(thread.Subject))
                {
                    thread.Subject = string.IsNullOrWhiteSpace(subject) ? "Pharmacist Support" : subject.Trim();
                }
            }

            var nextMessageId = thread.Messages.Count == 0 ? 1 : thread.Messages.Max(entry => entry.Id) + 1;
            thread.Messages.Add(new PharmacistMessageEntry
            {
                Id = nextMessageId,
                SenderName = customerName.Trim(),
                SenderRole = AppRoles.Customer,
                Body = body.Trim(),
                SentAtUtc = DateTime.UtcNow,
                IsReadByPharmacist = false,
                IsReadByCustomer = true
            });
            thread.UpdatedAtUtc = DateTime.UtcNow;

            await WriteAllInternalAsync(threads, cancellationToken);
            return thread.Id;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to send customer support message.");
            throw;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task MarkThreadAsReadAsync(int threadId, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var threads = await ReadAllInternalAsync(cancellationToken);
            var thread = threads.FirstOrDefault(candidate => candidate.Id == threadId);
            if (thread is null)
            {
                return;
            }

            foreach (var message in thread.Messages.Where(entry => !AppRoles.Matches(entry.SenderRole, AppRoles.Pharmacist)))
            {
                message.IsReadByPharmacist = true;
            }

            await WriteAllInternalAsync(threads, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task MarkThreadAsReadForCustomerAsync(int threadId, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var threads = await ReadAllInternalAsync(cancellationToken);
            var thread = threads.FirstOrDefault(candidate => candidate.Id == threadId);
            if (thread is null)
            {
                return;
            }

            foreach (var message in thread.Messages.Where(entry => !AppRoles.Matches(entry.SenderRole, AppRoles.Customer)))
            {
                message.IsReadByCustomer = true;
            }

            await WriteAllInternalAsync(threads, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<int> GetUnreadCountForCustomerAsync(string customerEmail, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(customerEmail))
        {
            return 0;
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            var threads = await ReadAllInternalAsync(cancellationToken);
            return threads
                .Where(thread => string.Equals(thread.CustomerEmail, customerEmail.Trim(), StringComparison.OrdinalIgnoreCase))
                .SelectMany(thread => thread.Messages)
                .Count(message => !message.IsReadByCustomer && !AppRoles.Matches(message.SenderRole, AppRoles.Customer));
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<List<PharmacistMessageThread>> ReadAllInternalAsync(CancellationToken cancellationToken)
    {
        EnsureStorage();
        var json = await File.ReadAllTextAsync(filePath, cancellationToken);
        var threads = JsonSerializer.Deserialize<List<PharmacistMessageThread>>(json, JsonOptions);
        if (threads is not null && threads.Count > 0)
        {
            return threads;
        }

        var seededThreads = CreateSeedThreads();
        await WriteAllInternalAsync(seededThreads, cancellationToken);
        return seededThreads;
    }

    private async Task WriteAllInternalAsync(List<PharmacistMessageThread> threads, CancellationToken cancellationToken)
    {
        EnsureStorage();
        var json = JsonSerializer.Serialize(threads, JsonOptions);
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

    private static List<PharmacistMessageThread> CreateSeedThreads() =>
    [
        new()
        {
            Id = 1,
            Subject = "Prescription turnaround reminders",
            CounterpartyName = "Admin Operations",
            CounterpartyRole = AppRoles.Admin,
            UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-20),
            Messages =
            [
                new PharmacistMessageEntry
                {
                    Id = 1,
                    SenderName = "Admin Operations",
                    SenderRole = AppRoles.Admin,
                    Body = "Please prioritize prescription orders that have been pending for more than 30 minutes.",
                    SentAtUtc = DateTime.UtcNow.AddMinutes(-20),
                    IsReadByPharmacist = false,
                    IsReadByCustomer = true
                }
            ]
        }
    ];

    private static bool HasCustomerParticipant(PharmacistMessageThread thread) =>
        !string.IsNullOrWhiteSpace(thread.CustomerEmail);
}
