using System.Text.Json;
using PharmacyPOS.Models;
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
                .Where(IsOrderThread)
                .OrderByDescending(thread => thread.UpdatedAtUtc)
                .ToList();
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<IReadOnlyList<PharmacistMessageThread>> GetCustomerThreadsAsync(
        string customerEmail,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(customerEmail))
        {
            return [];
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            var normalizedEmail = customerEmail.Trim();
            var threads = await ReadAllInternalAsync(cancellationToken);
            return threads
                .Where(IsOrderThread)
                .Where(thread => string.Equals(thread.CustomerEmail, normalizedEmail, StringComparison.OrdinalIgnoreCase))
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
            return threads.FirstOrDefault(thread => thread.Id == threadId && IsOrderThread(thread));
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<PharmacistMessageThread?> GetThreadByOrderAsync(
        string orderNumber,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(orderNumber))
        {
            return null;
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            var normalizedOrderNumber = orderNumber.Trim();
            var threads = await ReadAllInternalAsync(cancellationToken);
            return threads.FirstOrDefault(thread =>
                IsOrderThread(thread) &&
                string.Equals(thread.OrderNumber, normalizedOrderNumber, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<int> EnsureOrderThreadAsync(PharmacyOrder order, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(order.OrderNumber))
        {
            throw new InvalidOperationException("Order number is required before creating a chat thread.");
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            var threads = await ReadAllInternalAsync(cancellationToken);
            var normalizedOrderNumber = order.OrderNumber.Trim();
            var thread = threads.FirstOrDefault(candidate =>
                string.Equals(candidate.OrderNumber, normalizedOrderNumber, StringComparison.OrdinalIgnoreCase) ||
                (candidate.OrderId.HasValue && candidate.OrderId.Value == order.Id));

            if (thread is null)
            {
                thread = new PharmacistMessageThread
                {
                    Id = threads.Count == 0 ? 1 : threads.Max(candidate => candidate.Id) + 1
                };
                threads.Add(thread);
            }

            SyncThreadWithOrder(thread, order);
            await WriteAllInternalAsync(threads, cancellationToken);
            return thread.Id;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to create or update the order chat thread for {OrderNumber}.", order.OrderNumber);
            throw;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task SendMessageAsync(
        int threadId,
        string senderName,
        string senderRole,
        string body,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            throw new InvalidOperationException("Message body is required.");
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            var threads = await ReadAllInternalAsync(cancellationToken);
            var thread = threads.FirstOrDefault(candidate => candidate.Id == threadId && IsOrderThread(candidate))
                ?? throw new InvalidOperationException("Message thread was not found.");

            var normalizedRole = senderRole.Trim();
            var isCustomer = AppRoles.Matches(normalizedRole, AppRoles.Customer);
            var isPharmacist = AppRoles.Matches(normalizedRole, AppRoles.Pharmacist);
            var nextMessageId = thread.Messages.Count == 0 ? 1 : thread.Messages.Max(entry => entry.Id) + 1;

            thread.Messages.Add(new PharmacistMessageEntry
            {
                Id = nextMessageId,
                SenderName = string.IsNullOrWhiteSpace(senderName) ? "SafeMed User" : senderName.Trim(),
                SenderRole = normalizedRole,
                Body = body.Trim(),
                SentAtUtc = DateTime.UtcNow,
                IsReadByPharmacist = isPharmacist,
                IsReadByCustomer = isCustomer
            });
            thread.UpdatedAtUtc = DateTime.UtcNow;

            await WriteAllInternalAsync(threads, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to send the order chat message for thread {ThreadId}.", threadId);
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
            var thread = threads.FirstOrDefault(candidate => candidate.Id == threadId && IsOrderThread(candidate));
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
            var thread = threads.FirstOrDefault(candidate => candidate.Id == threadId && IsOrderThread(candidate));
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
            var normalizedEmail = customerEmail.Trim();
            var threads = await ReadAllInternalAsync(cancellationToken);
            return threads
                .Where(IsOrderThread)
                .Where(thread => string.Equals(thread.CustomerEmail, normalizedEmail, StringComparison.OrdinalIgnoreCase))
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
        return JsonSerializer.Deserialize<List<PharmacistMessageThread>>(json, JsonOptions) ?? [];
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

    private static void SyncThreadWithOrder(PharmacistMessageThread thread, PharmacyOrder order)
    {
        var createdAtUtc = order.CreatedAtUtc == default ? DateTime.UtcNow : order.CreatedAtUtc;

        thread.OrderId = order.Id;
        thread.OrderNumber = order.OrderNumber.Trim();
        thread.OrderReference = ResolveOrderReference(order);
        thread.OrderStatus = string.IsNullOrWhiteSpace(order.OrderStatus)
            ? FirstNonEmpty(thread.OrderStatus, "Pending")
            : order.OrderStatus.Trim();
        thread.PaymentStatus = string.IsNullOrWhiteSpace(order.Payment?.Status)
            ? FirstNonEmpty(thread.PaymentStatus, "Pending")
            : order.Payment.Status.Trim();
        thread.PrescriptionStatus = string.IsNullOrWhiteSpace(order.PrescriptionStatus)
            ? thread.PrescriptionStatus
            : order.PrescriptionStatus.Trim();
        thread.RequiresPrescription = order.RequiresPrescription;
        thread.Subject = $"Order {thread.OrderNumber}";
        thread.CounterpartyName = "SafeMed Pharmacist";
        thread.CounterpartyRole = AppRoles.Pharmacist;
        thread.CustomerName = FirstNonEmpty(order.CustomerFullName, thread.CustomerName);
        thread.CustomerEmail = FirstNonEmpty(order.CustomerEmail, thread.CustomerEmail);
        thread.CustomerPhone = FirstNonEmpty(order.CustomerPhoneNumber, thread.CustomerPhone);
        thread.CreatedAtUtc = thread.CreatedAtUtc == default ? createdAtUtc : thread.CreatedAtUtc;
        thread.UpdatedAtUtc = thread.UpdatedAtUtc == default ? createdAtUtc : thread.UpdatedAtUtc;
    }

    private static string ResolveOrderReference(PharmacyOrder order) =>
        !string.IsNullOrWhiteSpace(order.Payment?.ReferenceNumber)
            ? order.Payment.ReferenceNumber.Trim()
            : order.OrderNumber.Trim();

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private static bool IsOrderThread(PharmacistMessageThread thread) =>
        !string.IsNullOrWhiteSpace(thread.OrderNumber) &&
        !string.IsNullOrWhiteSpace(thread.CustomerEmail);
}
