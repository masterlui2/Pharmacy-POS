using PharmacyPOS.Models.Checkout;
using PharmacyPOS.Models.PayMongoApi;

namespace PharmacyPOS.Services;

public interface ICheckoutService
{
    Task<PlaceOrderResult> PlaceOrderAsync(
        PlaceOrderRequest request,
        string customerEmail,
        string? successReturnUrl,
        string? cancelReturnUrl,
        CancellationToken cancellationToken = default);

    Task<PlaceOrderResult> CreateMobileCheckoutSessionAsync(
        MobileCreateCheckoutSessionRequest request,
        string firebaseUid,
        string? verifiedCustomerEmail,
        string? successReturnUrl,
        string? cancelReturnUrl,
        CancellationToken cancellationToken = default);
}
