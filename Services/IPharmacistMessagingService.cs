using PharmacyPOS.Models.Admin;

namespace PharmacyPOS.Services;

public interface IPharmacistMessagingService
{
    Task<IReadOnlyList<PharmacistMessageThread>> GetThreadsAsync(CancellationToken cancellationToken = default);

    Task<PharmacistMessageThread?> GetThreadAsync(int threadId, CancellationToken cancellationToken = default);

    Task<PharmacistMessageThread?> GetCustomerThreadAsync(string customerEmail, CancellationToken cancellationToken = default);

    Task<int> SendMessageAsync(
        int? threadId,
        string subject,
        string senderName,
        string senderRole,
        string body,
        CancellationToken cancellationToken = default);

    Task<int> SendCustomerMessageAsync(
        string customerName,
        string customerEmail,
        string customerPhone,
        string subject,
        string body,
        CancellationToken cancellationToken = default);

    Task MarkThreadAsReadAsync(int threadId, CancellationToken cancellationToken = default);

    Task MarkThreadAsReadForCustomerAsync(int threadId, CancellationToken cancellationToken = default);

    Task<int> GetUnreadCountForCustomerAsync(string customerEmail, CancellationToken cancellationToken = default);
}
