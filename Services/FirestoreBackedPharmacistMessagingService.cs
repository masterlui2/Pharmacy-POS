using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PharmacyPOS.Data;
using PharmacyPOS.Helpers;
using PharmacyPOS.Models;
using PharmacyPOS.Models.Admin;
using PharmacyPOS.Models.Security;

namespace PharmacyPOS.Services;

public sealed class FirestoreBackedPharmacistMessagingService(
    FilePharmacistMessagingService localMessagingService,
    PharmacyPosDbContext dbContext,
    IFirebaseOrderChatService firebaseOrderChatService,
    IHttpContextAccessor httpContextAccessor,
    ILogger<FirestoreBackedPharmacistMessagingService> logger) : IPharmacistMessagingService
{
    private const int RecentOrderLimit = 50;

    public async Task<IReadOnlyList<PharmacistMessageThread>> GetThreadsAsync(CancellationToken cancellationToken = default)
    {
        var orders = await LoadRecentOrdersAsync(cancellationToken);
        await SyncOrdersAsync(orders, cancellationToken);
        return await localMessagingService.GetThreadsAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PharmacistMessageThread>> GetCustomerThreadsAsync(
        string customerEmail,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(customerEmail))
        {
            return [];
        }

        var orders = await LoadCustomerOrdersAsync(customerEmail.Trim(), cancellationToken);
        await SyncOrdersAsync(orders, cancellationToken);
        return await localMessagingService.GetCustomerThreadsAsync(customerEmail, cancellationToken);
    }

    public async Task<PharmacistMessageThread?> GetThreadAsync(int threadId, CancellationToken cancellationToken = default)
    {
        var thread = await localMessagingService.GetThreadAsync(threadId, cancellationToken);
        if (thread is null)
        {
            return null;
        }

        var order = await LoadOrderByNumberAsync(thread.OrderNumber, cancellationToken);
        if (order is not null)
        {
            await SyncOrdersAsync([order], cancellationToken);
        }

        return await localMessagingService.GetThreadAsync(threadId, cancellationToken);
    }

    public async Task<PharmacistMessageThread?> GetThreadByOrderAsync(
        string orderNumber,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(orderNumber))
        {
            return null;
        }

        var order = await LoadOrderByNumberAsync(orderNumber.Trim(), cancellationToken);
        if (order is not null)
        {
            await SyncOrdersAsync([order], cancellationToken);
        }

        return await localMessagingService.GetThreadByOrderAsync(orderNumber, cancellationToken);
    }

    public async Task<int> EnsureOrderThreadAsync(
        PharmacyOrder order,
        CancellationToken cancellationToken = default)
    {
        await SyncOrdersAsync([order], cancellationToken);
        var thread = await localMessagingService.GetThreadByOrderAsync(order.OrderNumber, cancellationToken);
        return thread?.Id ?? await localMessagingService.EnsureOrderThreadAsync(order, cancellationToken);
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

        var thread = await localMessagingService.GetThreadAsync(threadId, cancellationToken)
            ?? throw new InvalidOperationException("Message thread was not found.");
        var order = await LoadOrderByNumberAsync(thread.OrderNumber, cancellationToken);
        if (order is null || !CanUseFirestore(order))
        {
            await localMessagingService.SendMessageAsync(
                threadId,
                senderName,
                senderRole,
                body,
                cancellationToken);
            return;
        }

        if (AppRoles.Matches(senderRole, AppRoles.Pharmacist))
        {
            await firebaseOrderChatService.SendPharmacistReplyAsync(
                order,
                order.Payment,
                ResolveCurrentPharmacistIdentity(order, senderName),
                body,
                documentId: null,
                cancellationToken);
        }
        else if (AppRoles.Matches(senderRole, AppRoles.Customer))
        {
            var customerUid = FirstNonEmpty(order.CustomerUid, thread.CustomerUid);
            if (string.IsNullOrWhiteSpace(customerUid))
            {
                logger.LogWarning(
                    "Sending customer message for order {OrderNumber} to local chat storage because the order is not linked to a Firebase customer UID.",
                    order.OrderNumber);
                await localMessagingService.SendMessageAsync(
                    threadId,
                    senderName,
                    senderRole,
                    body,
                    cancellationToken);
                return;
            }

            try
            {
                await firebaseOrderChatService.SendCustomerMessageAsync(
                    order,
                    order.Payment,
                    customerUid,
                    FirstNonEmpty(senderName, order.CustomerFullName, "Customer"),
                    body,
                    documentId: null,
                    cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(
                    exception,
                    "Firestore customer message send failed for order {OrderNumber}; using local chat storage.",
                    order.OrderNumber);
                await localMessagingService.SendMessageAsync(
                    threadId,
                    senderName,
                    senderRole,
                    body,
                    cancellationToken);
                return;
            }
        }
        else
        {
            await localMessagingService.SendMessageAsync(
                threadId,
                senderName,
                senderRole,
                body,
                cancellationToken);
            return;
        }

        await SyncOrdersAsync([order], cancellationToken);
    }

    public Task MarkThreadAsReadAsync(int threadId, CancellationToken cancellationToken = default) =>
        localMessagingService.MarkThreadAsReadAsync(threadId, cancellationToken);

    public Task MarkThreadAsReadForCustomerAsync(int threadId, CancellationToken cancellationToken = default) =>
        localMessagingService.MarkThreadAsReadForCustomerAsync(threadId, cancellationToken);

    public async Task<int> GetUnreadCountForCustomerAsync(
        string customerEmail,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(customerEmail))
        {
            return 0;
        }

        var orders = await LoadCustomerOrdersAsync(customerEmail.Trim(), cancellationToken);
        await SyncOrdersAsync(orders, cancellationToken);
        return await localMessagingService.GetUnreadCountForCustomerAsync(customerEmail, cancellationToken);
    }

    private async Task<List<PharmacyOrder>> LoadRecentOrdersAsync(CancellationToken cancellationToken) =>
        await dbContext.Orders
            .AsNoTracking()
            .Include(order => order.Items)
            .Include(order => order.Payment)
            .Where(order =>
                !string.IsNullOrWhiteSpace(order.OrderNumber) &&
                (!string.IsNullOrWhiteSpace(order.CustomerEmail) || !string.IsNullOrWhiteSpace(order.CustomerUid)))
            .OrderByDescending(order => order.CreatedAtUtc)
            .Take(RecentOrderLimit)
            .ToListAsync(cancellationToken);

    private async Task<List<PharmacyOrder>> LoadCustomerOrdersAsync(
        string customerEmail,
        CancellationToken cancellationToken) =>
        await dbContext.Orders
            .AsNoTracking()
            .Include(order => order.Items)
            .Include(order => order.Payment)
            .Where(order => order.CustomerEmail == customerEmail)
            .OrderByDescending(order => order.CreatedAtUtc)
            .Take(RecentOrderLimit)
            .ToListAsync(cancellationToken);

    private async Task<PharmacyOrder?> LoadOrderByNumberAsync(
        string orderNumber,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(orderNumber))
        {
            return null;
        }

        return await dbContext.Orders
            .AsNoTracking()
            .Include(order => order.Items)
            .Include(order => order.Payment)
            .FirstOrDefaultAsync(order => order.OrderNumber == orderNumber, cancellationToken);
    }

    private async Task SyncOrdersAsync(
        IReadOnlyList<PharmacyOrder> orders,
        CancellationToken cancellationToken)
    {
        var normalizedOrders = orders
            .Where(order => !string.IsNullOrWhiteSpace(order.OrderNumber))
            .GroupBy(order => order.OrderNumber, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(order => order.CreatedAtUtc).First())
            .ToList();
        if (normalizedOrders.Count == 0)
        {
            return;
        }

        var candidateIdsByOrderNumber = normalizedOrders
            .Where(CanUseFirestore)
            .ToDictionary(
                order => order.OrderNumber,
                order => FirebaseOrderContract.ResolveDocumentIdCandidates(order, order.Payment),
                StringComparer.OrdinalIgnoreCase);
        var allDocumentIds = candidateIdsByOrderNumber.Values
            .SelectMany(documentIds => documentIds)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        IReadOnlyDictionary<string, IReadOnlyList<FirebaseOrderChatMessage>> messagesByDocumentId =
            new Dictionary<string, IReadOnlyList<FirebaseOrderChatMessage>>(StringComparer.Ordinal);
        if (allDocumentIds.Count > 0)
        {
            try
            {
                messagesByDocumentId = await firebaseOrderChatService.GetMessagesByOrderAsync(allDocumentIds, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(
                    exception,
                    "Firestore chat synchronization is unavailable; continuing with local message threads.");
            }
        }

        foreach (var order in normalizedOrders)
        {
            try
            {
                var importedMessages = BuildImportedMessages(
                    order,
                    candidateIdsByOrderNumber,
                    messagesByDocumentId);
                await localMessagingService.SyncExternalMessagesAsync(
                    order,
                    importedMessages,
                    cancellationToken);
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Failed to synchronize the order chat thread for {OrderNumber}.",
                    order.OrderNumber);
            }
        }
    }

    private static List<PharmacistMessageEntry> BuildImportedMessages(
        PharmacyOrder order,
        IReadOnlyDictionary<string, IReadOnlyList<string>> candidateIdsByOrderNumber,
        IReadOnlyDictionary<string, IReadOnlyList<FirebaseOrderChatMessage>> messagesByDocumentId)
    {
        if (!candidateIdsByOrderNumber.TryGetValue(order.OrderNumber, out var candidateIds))
        {
            return [];
        }

        IReadOnlyList<FirebaseOrderChatMessage> selectedMessages = [];
        foreach (var candidateId in candidateIds)
        {
            if (!messagesByDocumentId.TryGetValue(candidateId, out var candidateMessages))
            {
                continue;
            }

            selectedMessages = candidateMessages;
            if (candidateMessages.Count > 0)
            {
                break;
            }
        }

        return selectedMessages
            .Select(message => ToStoredMessage(order, message))
            .ToList();
    }

    private static PharmacistMessageEntry ToStoredMessage(
        PharmacyOrder order,
        FirebaseOrderChatMessage message)
    {
        var senderRole = NormalizeSenderRole(order, message);
        var (isReadByPharmacist, isReadByCustomer) = ResolveReadState(senderRole);

        return new PharmacistMessageEntry
        {
            ExternalMessageId = message.Id,
            SenderUid = message.SenderUid,
            SenderName = ResolveSenderName(order, senderRole, message.SenderName),
            SenderRole = senderRole,
            Body = message.Text,
            SentAtUtc = message.CreatedAtUtc ?? DateTime.UtcNow,
            IsReadByPharmacist = isReadByPharmacist,
            IsReadByCustomer = isReadByCustomer
        };
    }

    private static string NormalizeSenderRole(PharmacyOrder order, FirebaseOrderChatMessage message)
    {
        if (AppRoles.Matches(message.SenderRole, AppRoles.Customer))
        {
            return AppRoles.Customer;
        }

        if (AppRoles.Matches(message.SenderRole, AppRoles.Pharmacist))
        {
            return AppRoles.Pharmacist;
        }

        if (string.Equals(message.SenderRole, "system", StringComparison.OrdinalIgnoreCase))
        {
            return "system";
        }

        if (!string.IsNullOrWhiteSpace(order.CustomerUid) &&
            string.Equals(order.CustomerUid, message.SenderUid, StringComparison.Ordinal))
        {
            return AppRoles.Customer;
        }

        return string.IsNullOrWhiteSpace(message.SenderRole)
            ? "system"
            : message.SenderRole.Trim();
    }

    private static string ResolveSenderName(
        PharmacyOrder order,
        string senderRole,
        string senderName)
    {
        if (!string.IsNullOrWhiteSpace(senderName))
        {
            return senderName.Trim();
        }

        if (AppRoles.Matches(senderRole, AppRoles.Customer))
        {
            return FirstNonEmpty(order.CustomerFullName, "Customer");
        }

        if (AppRoles.Matches(senderRole, AppRoles.Pharmacist))
        {
            return "Pharmacist";
        }

        return "System";
    }

    private static (bool IsReadByPharmacist, bool IsReadByCustomer) ResolveReadState(string senderRole)
    {
        if (AppRoles.Matches(senderRole, AppRoles.Customer))
        {
            return (false, true);
        }

        if (AppRoles.Matches(senderRole, AppRoles.Pharmacist))
        {
            return (true, false);
        }

        return (true, false);
    }

    private static bool CanUseFirestore(PharmacyOrder order) =>
        !string.IsNullOrWhiteSpace(order.CustomerUid);

    private FirebasePharmacistIdentity ResolveCurrentPharmacistIdentity(
        PharmacyOrder order,
        string senderName)
    {
        var session = httpContextAccessor.HttpContext?.Session;
        var currentEmail = session?.GetString("Email") ?? string.Empty;
        var currentUsername = session?.GetString("Username") ?? string.Empty;

        return new FirebasePharmacistIdentity
        {
            Uid = string.IsNullOrWhiteSpace(currentEmail)
                ? ResolveFallbackStaffUid(FirstNonEmpty(senderName, currentUsername))
                : currentEmail.Trim().ToLowerInvariant(),
            Name = FirstNonEmpty(senderName, currentUsername, "Pharmacist"),
            PharmacyName = string.IsNullOrWhiteSpace(order.FulfillmentBranch)
                ? "SafeMed Pharmacy"
                : order.FulfillmentBranch.Trim()
        };
    }

    private static string ResolveFallbackStaffUid(string staffName)
    {
        if (string.IsNullOrWhiteSpace(staffName))
        {
            return "pharmacist";
        }

        var slugCharacters = staffName
            .Trim()
            .ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '-')
            .ToArray();
        var slug = new string(slugCharacters).Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? "pharmacist" : $"pharmacist-{slug}";
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
}
