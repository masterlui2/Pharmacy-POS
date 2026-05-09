using Google.Cloud.Firestore;
using Microsoft.Extensions.Options;
using PharmacyPOS.Helpers;
using PharmacyPOS.Models;

namespace PharmacyPOS.Services;

public class FirebaseSyncService(
    FirebaseAppInitializer firebaseAppInitializer,
    IOptions<FirebaseOptions> firebaseOptionsAccessor,
    ILogger<FirebaseSyncService> logger) : IFirebaseSyncService, IFirebaseOrderChatService
{
    private readonly FirestoreDb? firestore = firebaseAppInitializer.Firestore;
    private readonly FirebaseOptions firebaseOptions = firebaseOptionsAccessor.Value;

    public async Task SyncOrderAsync(
        PharmacyOrder order,
        PaymentRecord? payment,
        CancellationToken cancellationToken)
    {
        var firestoreDb = GetFirestoreForBackgroundSync(order.OrderNumber);
        if (firestoreDb is null)
        {
            return;
        }

        var documentId = FirebaseOrderContract.ResolveDocumentId(order, payment);
        var document = firestoreDb.Collection("orders").Document(documentId);
        var data = BuildOrderDocument(
            order,
            payment,
            ResolvePharmacyName(order),
            pharmacist: null,
            includeBlankPharmacist: true,
            includeLastMessageAt: false);

        await document.SetAsync(data, SetOptions.MergeAll, cancellationToken);
    }

    public async Task UpdateOrderStatusAsync(
        PharmacyOrder order,
        PaymentRecord? payment,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(order.OrderNumber))
        {
            logger.LogWarning("Skipped Firebase order status sync because the order number was empty.");
            return;
        }

        var firestoreDb = GetFirestoreForBackgroundSync(order.OrderNumber);
        if (firestoreDb is null)
        {
            return;
        }

        var documentId = await ResolveOrderDocumentIdAsync(
            firestoreDb,
            order,
            payment,
            documentIdOverride: null,
            cancellationToken);
        var document = firestoreDb.Collection("orders").Document(documentId);
        var data = BuildOrderDocument(
            order,
            payment,
            ResolvePharmacyName(order),
            pharmacist: null,
            includeBlankPharmacist: false,
            includeLastMessageAt: false);

        await document.SetAsync(data, SetOptions.MergeAll, cancellationToken);
    }

    public async Task CreateNotificationAsync(
        string customerUid,
        string orderNumber,
        string title,
        string message,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(customerUid))
        {
            logger.LogWarning(
                "Skipped Firebase notification for order {OrderNumber} because no customer UID was available.",
                orderNumber);
            return;
        }

        var firestoreDb = GetFirestoreForBackgroundSync(orderNumber);
        if (firestoreDb is null)
        {
            return;
        }

        var document = firestoreDb.Collection("notifications").Document();
        var data = new Dictionary<string, object?>
        {
            ["customerUid"] = customerUid,
            ["orderNumber"] = orderNumber,
            ["title"] = title,
            ["message"] = message,
            ["type"] = "order_status",
            ["isRead"] = false,
            ["createdAt"] = FieldValue.ServerTimestamp
        };

        await document.SetAsync(data, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<FirebaseOrderChatMessage>>> GetMessagesByOrderAsync(
        IEnumerable<string> orderIds,
        CancellationToken cancellationToken)
    {
        var normalizedOrderIds = orderIds
            .Where(orderId => !string.IsNullOrWhiteSpace(orderId))
            .Select(orderId => orderId.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (normalizedOrderIds.Count == 0)
        {
            return new Dictionary<string, IReadOnlyList<FirebaseOrderChatMessage>>(StringComparer.Ordinal);
        }

        var firestoreDb = firestore;
        if (firestoreDb is null)
        {
            logger.LogWarning(
                "Skipped Firestore order-message read because Firestore is unavailable. {Reason}",
                firebaseAppInitializer.FirestoreUnavailableReason ??
                firebaseAppInitializer.UnavailableReason ??
                "No additional details were provided.");
            return normalizedOrderIds.ToDictionary(
                orderId => orderId,
                _ => (IReadOnlyList<FirebaseOrderChatMessage>)[],
                StringComparer.Ordinal);
        }

        var loadTasks = normalizedOrderIds.Select(async orderId =>
        {
            try
            {
                var messages = await LoadMessagesAsync(firestoreDb, orderId, cancellationToken);
                return new KeyValuePair<string, IReadOnlyList<FirebaseOrderChatMessage>>(orderId, messages);
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Failed to read Firestore messages for order {OrderNumber}.",
                    orderId);
                return new KeyValuePair<string, IReadOnlyList<FirebaseOrderChatMessage>>(orderId, []);
            }
        });

        var results = await Task.WhenAll(loadTasks);
        return results.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
    }

    public async Task SendPharmacistReplyAsync(
        PharmacyOrder order,
        PaymentRecord? payment,
        FirebasePharmacistIdentity pharmacist,
        string body,
        string? documentId,
        CancellationToken cancellationToken) =>
        await SendOrderMessageAsyncInternal(
            order,
            payment,
            new FirebaseOrderChatMessageWriteRequest
            {
                SenderId = pharmacist.Uid,
                SenderRole = "pharmacist",
                SenderName = pharmacist.Name,
                RecipientRole = "customer",
                Text = body,
                Pharmacist = pharmacist
            },
            documentId,
            cancellationToken);

    public async Task SendCustomerMessageAsync(
        PharmacyOrder order,
        PaymentRecord? payment,
        string senderUid,
        string senderName,
        string body,
        string? documentId,
        CancellationToken cancellationToken) =>
        await SendOrderMessageAsyncInternal(
            order,
            payment,
            new FirebaseOrderChatMessageWriteRequest
            {
                SenderId = senderUid,
                SenderRole = "customer",
                SenderName = senderName,
                RecipientRole = "pharmacist",
                Text = body
            },
            documentId,
            cancellationToken);

    public async Task UpdatePharmacistAssignmentAsync(
        PharmacyOrder order,
        PaymentRecord? payment,
        FirebasePharmacistIdentity pharmacist,
        string? documentId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(order.OrderNumber))
        {
            throw new InvalidOperationException("Order number is required before updating pharmacist assignment.");
        }

        var firestoreDb = GetRequiredFirestore(order.OrderNumber);
        var pharmacistIdentity = ResolvePharmacistIdentity(order, pharmacist);
        var resolvedDocumentId = await ResolveOrderDocumentIdAsync(
            firestoreDb,
            order,
            payment,
            documentId,
            cancellationToken);
        var orderDocument = firestoreDb.Collection("orders").Document(resolvedDocumentId);

        await orderDocument.SetAsync(
            BuildOrderDocument(
                order,
                payment,
                pharmacistIdentity.PharmacyName,
                pharmacistIdentity,
                includeBlankPharmacist: true,
                includeLastMessageAt: false),
            SetOptions.MergeAll,
            cancellationToken);
    }

    private async Task<IReadOnlyList<FirebaseOrderChatMessage>> LoadMessagesAsync(
        FirestoreDb firestoreDb,
        string orderId,
        CancellationToken cancellationToken)
    {
        var snapshot = await firestoreDb
            .Collection("orders")
            .Document(orderId)
            .Collection("messages")
            .GetSnapshotAsync(cancellationToken);

        return snapshot.Documents
            .Select(MapMessage)
            .OrderBy(message => message.CreatedAtUtc ?? DateTime.MinValue)
            .ToList();
    }

    private FirestoreDb? GetFirestoreForBackgroundSync(string orderNumber)
    {
        if (firestore is not null)
        {
            return firestore;
        }

        logger.LogWarning(
            "Skipped Firebase sync for order {OrderNumber} because Firebase is unavailable. {Reason}",
            orderNumber,
            firebaseAppInitializer.FirestoreUnavailableReason ??
            firebaseAppInitializer.UnavailableReason ??
            "No additional details were provided.");
        return null;
    }

    private FirestoreDb GetRequiredFirestore(string orderNumber)
    {
        if (firestore is not null)
        {
            return firestore;
        }

        throw new InvalidOperationException(
            $"Cloud Firestore is unavailable for order {orderNumber}. " +
            (firebaseAppInitializer.FirestoreUnavailableReason ??
             firebaseAppInitializer.UnavailableReason ??
             "No additional details were provided."));
    }

    private Dictionary<string, object?> BuildOrderDocument(
        PharmacyOrder order,
        PaymentRecord? payment,
        string pharmacyName,
        FirebasePharmacistIdentity? pharmacist,
        bool includeBlankPharmacist,
        bool includeLastMessageAt)
    {
        var orderReference = FirebaseOrderContract.ResolveOrderReference(order, payment);
        var createdAt = NormalizeTimestamp(order.CreatedAtUtc);
        var data = new Dictionary<string, object?>
        {
            ["orderNumber"] = order.OrderNumber,
            ["customerUid"] = order.CustomerUid,
            ["customerEmail"] = order.CustomerEmail,
            ["customerName"] = order.CustomerFullName,
            ["customerPhone"] = order.CustomerPhoneNumber,
            ["referenceNumber"] = orderReference,
            ["reference_number"] = orderReference,
            ["orderReference"] = orderReference,
            ["status"] = order.OrderStatus,
            ["orderStatus"] = order.OrderStatus,
            ["paymentStatus"] = payment?.Status ?? "Pending",
            ["paymentMethod"] = order.PaymentMethod,
            ["pharmacyName"] = pharmacyName,
            ["pharmacy"] = new Dictionary<string, object?>
            {
                ["name"] = pharmacyName
            },
            ["requiresPrescription"] = order.RequiresPrescription,
            ["prescriptionRequired"] = order.RequiresPrescription,
            ["totalAmount"] = ToFirestoreNumber(order.TotalAmount),
            ["subtotalAmount"] = ToFirestoreNumber(order.SubtotalAmount),
            ["taxAmount"] = ToFirestoreNumber(order.TaxAmount),
            ["shippingAmount"] = ToFirestoreNumber(order.ShippingAmount),
            ["discountAmount"] = ToFirestoreNumber(order.DiscountAmount),
            ["deliveryAddress"] = order.DeliveryAddress,
            ["deliveryOption"] = order.DeliveryOption,
            ["fulfillmentBranch"] = order.FulfillmentBranch,
            ["prescriptionStatus"] = order.PrescriptionStatus,
            ["createdAt"] = createdAt,
            ["updatedAt"] = FieldValue.ServerTimestamp,
            ["items"] = order.Items
                .OrderBy(item => item.Id)
                .Select(item => new Dictionary<string, object?>
                {
                    ["productId"] = item.ProductId,
                    ["productName"] = item.ProductName,
                    ["brandName"] = item.BrandName,
                    ["imageUrl"] = item.ImageUrl,
                    ["quantity"] = item.Quantity,
                    ["unitPrice"] = ToFirestoreNumber(item.UnitPrice),
                    ["taxAmount"] = ToFirestoreNumber(item.TaxAmount),
                    ["requiresPrescription"] = item.RequiresPrescription,
                    ["lineTotal"] = ToFirestoreNumber(item.UnitPrice * item.Quantity)
                })
                .ToList()
        };

        if (pharmacist is not null || includeBlankPharmacist)
        {
            var pharmacistUid = pharmacist?.Uid?.Trim() ?? string.Empty;
            var pharmacistName = pharmacist?.Name?.Trim() ?? string.Empty;
            data["pharmacistUid"] = pharmacistUid;
            data["pharmacistName"] = pharmacistName;
            data["pharmacist"] = new Dictionary<string, object?>
            {
                ["uid"] = pharmacistUid,
                ["name"] = pharmacistName
            };
        }

        if (includeLastMessageAt)
        {
            data["lastMessageAt"] = FieldValue.ServerTimestamp;
        }

        return data;
    }

    private FirebasePharmacistIdentity ResolvePharmacistIdentity(
        PharmacyOrder order,
        FirebasePharmacistIdentity? pharmacist)
    {
        var pharmacyName = FirstNonEmpty(
            pharmacist?.PharmacyName,
            ResolvePharmacyName(order));
        var pharmacistName = FirstNonEmpty(
            pharmacist?.Name,
            firebaseOptions.DefaultPharmacistName);
        var pharmacistUid = FirstNonEmpty(
            pharmacist?.Uid,
            firebaseOptions.DefaultPharmacistUid);

        return new FirebasePharmacistIdentity
        {
            Uid = pharmacistUid,
            Name = pharmacistName,
            PharmacyName = pharmacyName
        };
    }

    private string ResolvePharmacyName(PharmacyOrder order) =>
        FirstNonEmpty(
            firebaseOptions.PharmacyName,
            order.FulfillmentBranch,
            "SafeMed Pharmacy");

    private async Task SendOrderMessageAsyncInternal(
        PharmacyOrder order,
        PaymentRecord? payment,
        FirebaseOrderChatMessageWriteRequest request,
        string? documentId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(order.OrderNumber))
        {
            throw new InvalidOperationException("Order number is required before sending an order message.");
        }

        if (string.IsNullOrWhiteSpace(request.Text))
        {
            throw new InvalidOperationException("Message body is required before sending an order message.");
        }

        var firestoreDb = GetRequiredFirestore(order.OrderNumber);
        var resolvedDocumentId = await ResolveOrderDocumentIdAsync(
            firestoreDb,
            order,
            payment,
            documentId,
            cancellationToken);
        var orderReference = FirebaseOrderContract.ResolveOrderReference(order, payment);
        var orderDocument = firestoreDb.Collection("orders").Document(resolvedDocumentId);
        var messageDocument = orderDocument.Collection("messages").Document();
        var batch = firestoreDb.StartBatch();
        var senderId = request.SenderId.Trim();
        var senderRole = request.SenderRole.Trim().ToLowerInvariant();
        var senderName = request.SenderName.Trim();
        var recipientRole = request.RecipientRole.Trim().ToLowerInvariant();

        batch.Set(
            messageDocument,
            new Dictionary<string, object?>
            {
                ["type"] = "text",
                ["text"] = request.Text.Trim(),
                ["orderId"] = resolvedDocumentId,
                ["orderReference"] = orderReference,
                ["senderId"] = senderId,
                ["senderUid"] = senderId,
                ["senderRole"] = senderRole,
                ["senderName"] = senderName,
                ["recipientRole"] = recipientRole,
                ["createdAt"] = FieldValue.ServerTimestamp
            });

        Dictionary<string, object?> orderData;
        if (string.Equals(senderRole, "pharmacist", StringComparison.OrdinalIgnoreCase))
        {
            var pharmacistIdentity = ResolvePharmacistIdentity(order, request.Pharmacist);
            orderData = BuildOrderDocument(
                order,
                payment,
                pharmacistIdentity.PharmacyName,
                pharmacistIdentity,
                includeBlankPharmacist: true,
                includeLastMessageAt: true);
        }
        else
        {
            orderData = BuildOrderDocument(
                order,
                payment,
                ResolvePharmacyName(order),
                pharmacist: null,
                includeBlankPharmacist: false,
                includeLastMessageAt: true);
        }

        batch.Set(orderDocument, orderData, SetOptions.MergeAll);
        await batch.CommitAsync(cancellationToken);
    }

    private async Task<string> ResolveOrderDocumentIdAsync(
        FirestoreDb firestoreDb,
        PharmacyOrder order,
        PaymentRecord? payment,
        string? documentIdOverride,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(documentIdOverride))
        {
            return documentIdOverride.Trim();
        }

        var candidateIds = FirebaseOrderContract.ResolveDocumentIdCandidates(order, payment);
        if (candidateIds.Count <= 1)
        {
            return candidateIds.FirstOrDefault() ?? order.OrderNumber;
        }

        foreach (var candidateId in candidateIds)
        {
            var messageSnapshot = await firestoreDb
                .Collection("orders")
                .Document(candidateId)
                .Collection("messages")
                .Limit(1)
                .GetSnapshotAsync(cancellationToken);
            if (messageSnapshot.Count > 0)
            {
                return candidateId;
            }
        }

        foreach (var candidateId in candidateIds)
        {
            var orderSnapshot = await firestoreDb
                .Collection("orders")
                .Document(candidateId)
                .GetSnapshotAsync(cancellationToken);
            if (orderSnapshot.Exists)
            {
                return candidateId;
            }
        }

        return candidateIds.First();
    }

    private static FirebaseOrderChatMessage MapMessage(DocumentSnapshot snapshot) =>
        new()
        {
            Id = snapshot.Id,
            Type = ReadString(snapshot, "type") ?? "text",
            Text = ReadString(snapshot, "text", "body", "message", "content") ?? string.Empty,
            OrderId = ReadString(snapshot, "orderId", "order_id") ?? string.Empty,
            OrderReference = ReadString(snapshot, "orderReference", "referenceNumber", "reference_number") ?? string.Empty,
            SenderUid = ReadString(snapshot, "senderId", "senderUid", "sender.uid") ?? string.Empty,
            SenderRole = ReadString(snapshot, "senderRole") ?? string.Empty,
            SenderName = ReadString(snapshot, "senderName", "sender.name") ?? string.Empty,
            RecipientRole = ReadString(snapshot, "recipientRole") ?? string.Empty,
            CreatedAtUtc = ReadTimestamp(snapshot, "createdAt", "timestamp", "sentAt", "sent_at")
        };

    private static DateTime NormalizeTimestamp(DateTime timestamp) =>
        timestamp.Kind switch
        {
            DateTimeKind.Utc => timestamp,
            DateTimeKind.Local => timestamp.ToUniversalTime(),
            _ => DateTime.SpecifyKind(timestamp, DateTimeKind.Utc)
        };

    private static DateTime? ReadTimestamp(DocumentSnapshot snapshot, params string[] fieldPaths)
    {
        foreach (var fieldPath in fieldPaths)
        {
            if (snapshot.TryGetValue(fieldPath, out Timestamp timestamp))
            {
                return timestamp.ToDateTime();
            }

            if (snapshot.TryGetValue(fieldPath, out DateTime dateTime))
            {
                return NormalizeTimestamp(dateTime);
            }
        }

        return null;
    }

    private static string? ReadString(DocumentSnapshot snapshot, params string[] fieldPaths)
    {
        foreach (var fieldPath in fieldPaths)
        {
            if (snapshot.TryGetValue(fieldPath, out string value) &&
                !string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private static double ToFirestoreNumber(decimal value) =>
        decimal.ToDouble(Math.Round(value, 2, MidpointRounding.AwayFromZero));
}
