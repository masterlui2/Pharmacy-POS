using PharmacyPOS.Models;

namespace PharmacyPOS.Services;

public interface IFirebaseSyncService
{
    Task SyncOrderAsync(PharmacyOrder order, PaymentRecord? payment, CancellationToken cancellationToken);

    Task UpdateOrderStatusAsync(
        PharmacyOrder order,
        PaymentRecord? payment,
        CancellationToken cancellationToken);

    Task CreateNotificationAsync(
        string customerUid,
        string orderNumber,
        string title,
        string message,
        CancellationToken cancellationToken);
}
