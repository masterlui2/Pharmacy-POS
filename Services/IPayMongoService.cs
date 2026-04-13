using PharmacyPOS.Models;

namespace PharmacyPOS.Services;

public interface IPayMongoService
{
    Task<PayMongoCheckoutSessionResult> CreateCheckoutSessionAsync(
        PharmacyOrder order,
        IReadOnlyList<PharmacyOrderItem> items,
        string paymentMethod,
        CancellationToken cancellationToken = default);

    bool IsConfigured();
}
