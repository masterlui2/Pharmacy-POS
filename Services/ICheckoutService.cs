using PharmacyPOS.Models.Checkout;

namespace PharmacyPOS.Services;

public interface ICheckoutService
{
    Task<PlaceOrderResult> PlaceOrderAsync(
        PlaceOrderRequest request,
        string customerEmail,
        string? successReturnUrl,
        string? cancelReturnUrl,
        CancellationToken cancellationToken = default);
}
