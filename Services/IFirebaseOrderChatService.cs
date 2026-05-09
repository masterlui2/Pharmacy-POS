using PharmacyPOS.Models;

namespace PharmacyPOS.Services;

public interface IFirebaseOrderChatService
{
    Task<IReadOnlyDictionary<string, IReadOnlyList<FirebaseOrderChatMessage>>> GetMessagesByOrderAsync(
        IEnumerable<string> orderIds,
        CancellationToken cancellationToken);

    Task SendCustomerMessageAsync(
        PharmacyOrder order,
        PaymentRecord? payment,
        string senderUid,
        string senderName,
        string body,
        string? documentId,
        CancellationToken cancellationToken);

    Task SendPharmacistReplyAsync(
        PharmacyOrder order,
        PaymentRecord? payment,
        FirebasePharmacistIdentity pharmacist,
        string body,
        string? documentId,
        CancellationToken cancellationToken);

    Task UpdatePharmacistAssignmentAsync(
        PharmacyOrder order,
        PaymentRecord? payment,
        FirebasePharmacistIdentity pharmacist,
        string? documentId,
        CancellationToken cancellationToken);
}
