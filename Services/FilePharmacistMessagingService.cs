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

    public async Task SyncExternalMessagesAsync(
        PharmacyOrder order,
        IReadOnlyList<PharmacistMessageEntry> externalMessages,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(order.OrderNumber))
        {
            throw new InvalidOperationException("Order number is required before synchronizing a chat thread.");
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            var threads = await ReadAllInternalAsync(cancellationToken);
            var normalizedOrderNumber = order.OrderNumber.Trim();
            var thread = threads.FirstOrDefault(candidate =>
                string.Equals(candidate.OrderNumber, normalizedOrderNumber, StringComparison.OrdinalIgnoreCase) ||
                (candidate.OrderId.HasValue && candidate.OrderId.Value == order.Id));

            var hasChanges = false;
            if (thread is null)
            {
                thread = new PharmacistMessageThread
                {
                    Id = threads.Count == 0 ? 1 : threads.Max(candidate => candidate.Id) + 1
                };
                threads.Add(thread);
                hasChanges = true;
            }

            hasChanges |= SyncThreadWithOrder(thread, order);
            hasChanges |= MergeExternalMessages(thread, externalMessages);

            if (hasChanges)
            {
                await WriteAllInternalAsync(threads, cancellationToken);
            }
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to synchronize Firestore order chat messages for {OrderNumber}.",
                order.OrderNumber);
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

    private static bool SyncThreadWithOrder(PharmacistMessageThread thread, PharmacyOrder order)
    {
        var createdAtUtc = order.CreatedAtUtc == default ? DateTime.UtcNow : order.CreatedAtUtc;
        var orderNumber = order.OrderNumber.Trim();
        var orderReference = ResolveOrderReference(order);
        var orderStatus = string.IsNullOrWhiteSpace(order.OrderStatus)
            ? FirstNonEmpty(thread.OrderStatus, "Pending")
            : order.OrderStatus.Trim();
        var paymentStatus = string.IsNullOrWhiteSpace(order.Payment?.Status)
            ? FirstNonEmpty(thread.PaymentStatus, "Pending")
            : order.Payment.Status.Trim();
        var prescriptionStatus = string.IsNullOrWhiteSpace(order.PrescriptionStatus)
            ? thread.PrescriptionStatus
            : order.PrescriptionStatus.Trim();
        var customerName = FirstNonEmpty(order.CustomerFullName, thread.CustomerName);
        var customerUid = FirstNonEmpty(order.CustomerUid, thread.CustomerUid);
        var customerEmail = FirstNonEmpty(order.CustomerEmail, thread.CustomerEmail);
        var customerPhone = FirstNonEmpty(order.CustomerPhoneNumber, thread.CustomerPhone);
        var createdTimestamp = thread.CreatedAtUtc == default ? createdAtUtc : thread.CreatedAtUtc;
        var updatedTimestamp = thread.UpdatedAtUtc == default ? createdAtUtc : thread.UpdatedAtUtc;
        var hasChanges = false;

        hasChanges |= thread.OrderId != order.Id;
        thread.OrderId = order.Id;

        hasChanges |= !string.Equals(thread.OrderNumber, orderNumber, StringComparison.Ordinal);
        thread.OrderNumber = orderNumber;

        hasChanges |= !string.Equals(thread.OrderReference, orderReference, StringComparison.Ordinal);
        thread.OrderReference = orderReference;

        hasChanges |= !string.Equals(thread.OrderStatus, orderStatus, StringComparison.Ordinal);
        thread.OrderStatus = orderStatus;

        hasChanges |= !string.Equals(thread.PaymentStatus, paymentStatus, StringComparison.Ordinal);
        thread.PaymentStatus = paymentStatus;

        hasChanges |= !string.Equals(thread.PrescriptionStatus, prescriptionStatus, StringComparison.Ordinal);
        thread.PrescriptionStatus = prescriptionStatus;

        hasChanges |= thread.RequiresPrescription != order.RequiresPrescription;
        thread.RequiresPrescription = order.RequiresPrescription;

        hasChanges |= !string.Equals(thread.Subject, $"Order {orderNumber}", StringComparison.Ordinal);
        thread.Subject = $"Order {orderNumber}";

        hasChanges |= !string.Equals(thread.CounterpartyName, "SafeMed Pharmacist", StringComparison.Ordinal);
        thread.CounterpartyName = "SafeMed Pharmacist";

        hasChanges |= !string.Equals(thread.CounterpartyRole, AppRoles.Pharmacist, StringComparison.Ordinal);
        thread.CounterpartyRole = AppRoles.Pharmacist;

        hasChanges |= !string.Equals(thread.CustomerName, customerName, StringComparison.Ordinal);
        thread.CustomerName = customerName;

        hasChanges |= !string.Equals(thread.CustomerUid, customerUid, StringComparison.Ordinal);
        thread.CustomerUid = customerUid;

        hasChanges |= !string.Equals(thread.CustomerEmail, customerEmail, StringComparison.OrdinalIgnoreCase);
        thread.CustomerEmail = customerEmail;

        hasChanges |= !string.Equals(thread.CustomerPhone, customerPhone, StringComparison.Ordinal);
        thread.CustomerPhone = customerPhone;

        hasChanges |= thread.CreatedAtUtc != createdTimestamp;
        thread.CreatedAtUtc = createdTimestamp;

        hasChanges |= thread.UpdatedAtUtc != updatedTimestamp;
        thread.UpdatedAtUtc = updatedTimestamp;

        return hasChanges;
    }

    private static bool MergeExternalMessages(
        PharmacistMessageThread thread,
        IReadOnlyList<PharmacistMessageEntry> externalMessages)
    {
        if (externalMessages.Count == 0)
        {
            return false;
        }

        var hasChanges = false;
        var nextMessageId = thread.Messages.Count == 0 ? 1 : thread.Messages.Max(entry => entry.Id) + 1;
        var existingByExternalId = thread.Messages
            .Where(entry => !string.IsNullOrWhiteSpace(entry.ExternalMessageId))
            .ToDictionary(entry => entry.ExternalMessageId, StringComparer.Ordinal);

        foreach (var importedMessage in externalMessages
                     .OrderBy(entry => entry.SentAtUtc)
                     .ThenBy(entry => entry.ExternalMessageId, StringComparer.Ordinal))
        {
            if (!string.IsNullOrWhiteSpace(importedMessage.ExternalMessageId) &&
                existingByExternalId.TryGetValue(importedMessage.ExternalMessageId, out var existingMessage))
            {
                hasChanges |= ApplyExternalMessage(existingMessage, importedMessage);
                continue;
            }

            var clonedMessage = CloneExternalMessage(importedMessage, nextMessageId++);
            thread.Messages.Add(clonedMessage);
            if (!string.IsNullOrWhiteSpace(clonedMessage.ExternalMessageId))
            {
                existingByExternalId[clonedMessage.ExternalMessageId] = clonedMessage;
            }

            hasChanges = true;
        }

        var latestMessageTimestamp = thread.Messages
            .Select(entry => entry.SentAtUtc)
            .DefaultIfEmpty(thread.UpdatedAtUtc)
            .Max();
        if (latestMessageTimestamp > thread.UpdatedAtUtc)
        {
            thread.UpdatedAtUtc = latestMessageTimestamp;
            hasChanges = true;
        }

        return hasChanges;
    }

    private static PharmacistMessageEntry CloneExternalMessage(
        PharmacistMessageEntry source,
        int id) =>
        new()
        {
            Id = id,
            ExternalMessageId = source.ExternalMessageId,
            SenderUid = source.SenderUid,
            SenderName = source.SenderName,
            SenderRole = source.SenderRole,
            Body = source.Body,
            SentAtUtc = source.SentAtUtc,
            IsReadByPharmacist = source.IsReadByPharmacist,
            IsReadByCustomer = source.IsReadByCustomer
        };

    private static bool ApplyExternalMessage(
        PharmacistMessageEntry target,
        PharmacistMessageEntry source)
    {
        var hasChanges = false;

        hasChanges |= !string.Equals(target.SenderUid, source.SenderUid, StringComparison.Ordinal);
        target.SenderUid = source.SenderUid;

        hasChanges |= !string.Equals(target.SenderName, source.SenderName, StringComparison.Ordinal);
        target.SenderName = source.SenderName;

        hasChanges |= !string.Equals(target.SenderRole, source.SenderRole, StringComparison.Ordinal);
        target.SenderRole = source.SenderRole;

        hasChanges |= !string.Equals(target.Body, source.Body, StringComparison.Ordinal);
        target.Body = source.Body;

        hasChanges |= target.SentAtUtc != source.SentAtUtc;
        target.SentAtUtc = source.SentAtUtc;

        return hasChanges;
    }

    private static string ResolveOrderReference(PharmacyOrder order) =>
        !string.IsNullOrWhiteSpace(order.Payment?.ReferenceNumber)
            ? order.Payment.ReferenceNumber.Trim()
            : order.OrderNumber.Trim();

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private static bool IsOrderThread(PharmacistMessageThread thread) =>
        !string.IsNullOrWhiteSpace(thread.OrderNumber) &&
        (!string.IsNullOrWhiteSpace(thread.CustomerEmail) ||
         !string.IsNullOrWhiteSpace(thread.CustomerUid));
}
