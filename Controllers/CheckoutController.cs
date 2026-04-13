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
        var result = await checkoutService.PlaceOrderAsync(request, customerEmail, cancellationToken);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Json(result);
    }
}
