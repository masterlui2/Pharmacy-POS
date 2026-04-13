using Microsoft.AspNetCore.Mvc;
using PharmacyPOS.Models.Checkout;
using PharmacyPOS.Services;

namespace PharmacyPOS.Controllers;

[Route("checkout")]
public class CheckoutController(ICheckoutService checkoutService) : Controller
{
    [HttpPost("place-order")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PlaceOrder([FromBody] PlaceOrderRequest request, CancellationToken cancellationToken)
    {
        var customerEmail = HttpContext.Session.GetString("Email") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(customerEmail))
        {
            return Unauthorized(new PlaceOrderResult
            {
                Success = false,
                Message = "Sign in to continue with checkout."
            });
        }

        var successReturnUrl = Url.Action("Index", "Orders", values: null, protocol: Request.Scheme);
        var cancelReturnUrl = Url.Action("Cart", "Home", values: null, protocol: Request.Scheme);
        var result = await checkoutService.PlaceOrderAsync(
            request,
            customerEmail,
            successReturnUrl,
            cancelReturnUrl,
            cancellationToken);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Json(result);
    }
}
