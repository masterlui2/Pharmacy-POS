using PharmacyPOS.Models;

namespace PharmacyPOS.Services;

public interface IPayMongoService
{
    Task<PayMongoCheckoutSessionResult> CreateCheckoutSessionAsync(
        PharmacyOrder order,
        IReadOnlyList<PharmacyOrderItem> items,
        string paymentMethod,
        string? successReturnUrl,
        string? cancelReturnUrl,
        CancellationToken cancellationToken = default);

    bool IsConfigured();
}
