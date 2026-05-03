using Google.Cloud.Firestore;
using PharmacyPOS.Models;

namespace PharmacyPOS.Services;

public class FirebaseSyncService(
    FirebaseAppInitializer firebaseAppInitializer,
    ILogger<FirebaseSyncService> logger) : IFirebaseSyncService
{
    private readonly FirestoreDb? firestore = firebaseAppInitializer.Firestore;

    public async Task SyncOrderAsync(
        PharmacyOrder order,
        PaymentRecord? payment,
        CancellationToken cancellationToken)
    {
        var firestoreDb = GetFirestore(order.OrderNumber);
        if (firestoreDb is null)
        {
            return;
        }

        var document = firestoreDb.Collection("orders").Document(order.OrderNumber);
        var snapshot = await document.GetSnapshotAsync(cancellationToken);
        var data = BuildOrderDocument(order, payment);

        if (!snapshot.Exists)
        {
            data["createdAt"] = FieldValue.ServerTimestamp;
        }

        data["updatedAt"] = FieldValue.ServerTimestamp;

        await document.SetAsync(data, SetOptions.MergeAll, cancellationToken);
    }

    public async Task UpdateOrderStatusAsync(
        string orderNumber,
        string status,
        string paymentStatus,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(orderNumber))
        {
            logger.LogWarning("Skipped Firebase order status sync because the order number was empty.");
            return;
        }

        var firestoreDb = GetFirestore(orderNumber);
        if (firestoreDb is null)
        {
            return;
        }

        var document = firestoreDb.Collection("orders").Document(orderNumber);
        var data = new Dictionary<string, object?>
        {
            ["orderNumber"] = orderNumber,
            ["status"] = status,
            ["paymentStatus"] = paymentStatus,
            ["updatedAt"] = FieldValue.ServerTimestamp
        };

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

        var firestoreDb = GetFirestore(orderNumber);
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

    private FirestoreDb? GetFirestore(string orderNumber)
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

    private static Dictionary<string, object?> BuildOrderDocument(PharmacyOrder order, PaymentRecord? payment) =>
        new()
        {
            ["orderNumber"] = order.OrderNumber,
            ["customerUid"] = order.CustomerUid,
            ["customerEmail"] = order.CustomerEmail,
            ["customerName"] = order.CustomerFullName,
            ["customerPhone"] = order.CustomerPhoneNumber,
            ["status"] = order.OrderStatus,
            ["paymentStatus"] = payment?.Status ?? "Pending",
            ["paymentMethod"] = order.PaymentMethod,
            ["totalAmount"] = order.TotalAmount,
            ["subtotalAmount"] = order.SubtotalAmount,
            ["taxAmount"] = order.TaxAmount,
            ["shippingAmount"] = order.ShippingAmount,
            ["discountAmount"] = order.DiscountAmount,
            ["deliveryAddress"] = order.DeliveryAddress,
            ["deliveryOption"] = order.DeliveryOption,
            ["fulfillmentBranch"] = order.FulfillmentBranch,
            ["prescriptionStatus"] = order.PrescriptionStatus,
            ["items"] = order.Items
                .OrderBy(item => item.Id)
                .Select(item => new Dictionary<string, object?>
                {
                    ["productId"] = item.ProductId,
                    ["productName"] = item.ProductName,
                    ["brandName"] = item.BrandName,
                    ["imageUrl"] = item.ImageUrl,
                    ["quantity"] = item.Quantity,
                    ["unitPrice"] = item.UnitPrice,
                    ["taxAmount"] = item.TaxAmount,
                    ["requiresPrescription"] = item.RequiresPrescription,
                    ["lineTotal"] = item.UnitPrice * item.Quantity
                })
                .ToList()
        };
}
