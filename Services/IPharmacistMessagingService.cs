using PharmacyPOS.Models;
using PharmacyPOS.Models.Admin;

namespace PharmacyPOS.Services;

public interface IPharmacistMessagingService
{
    Task<IReadOnlyList<PharmacistMessageThread>> GetThreadsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PharmacistMessageThread>> GetCustomerThreadsAsync(
        string customerEmail,
        CancellationToken cancellationToken = default);

    Task<PharmacistMessageThread?> GetThreadAsync(int threadId, CancellationToken cancellationToken = default);

    Task<PharmacistMessageThread?> GetThreadByOrderAsync(
        string orderNumber,
        CancellationToken cancellationToken = default);

    Task<int> EnsureOrderThreadAsync(
        PharmacyOrder order,
        CancellationToken cancellationToken = default);

    Task SendMessageAsync(
        int threadId,
        string senderName,
        string senderRole,
        string body,
        CancellationToken cancellationToken = default);

    Task MarkThreadAsReadAsync(int threadId, CancellationToken cancellationToken = default);

    Task MarkThreadAsReadForCustomerAsync(int threadId, CancellationToken cancellationToken = default);

    Task<int> GetUnreadCountForCustomerAsync(string customerEmail, CancellationToken cancellationToken = default);
}
