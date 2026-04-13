using Microsoft.AspNetCore.Mvc;
using PharmacyPOS.Models.Wishlist;
using PharmacyPOS.Services;

namespace PharmacyPOS.Controllers;

[Route("wishlist")]
public class WishlistController(IWishlistService wishlistService) : Controller
{
    [HttpGet("items")]
    public async Task<IActionResult> Items(CancellationToken cancellationToken)
    {
        var customerEmail = HttpContext.Session.GetString("Email") ?? string.Empty;
        var result = await wishlistService.GetItemsAsync(customerEmail, cancellationToken);
        if (!result.Success)
        {
            return Unauthorized(result);
        }

        return Json(result);
    }

    [HttpPost("toggle")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle([FromBody] WishlistToggleRequest request, CancellationToken cancellationToken)
    {
        var customerEmail = HttpContext.Session.GetString("Email") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(customerEmail))
        {
            return Unauthorized(new WishlistToggleResult
            {
                Success = false,
                Message = "Sign in to save medicines to your wishlist."
            });
        }

        var result = await wishlistService.ToggleAsync(customerEmail, request, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Json(result);
    }

    [HttpPost("remove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove([FromBody] WishlistToggleRequest request, CancellationToken cancellationToken)
    {
        var customerEmail = HttpContext.Session.GetString("Email") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(customerEmail))
        {
            return Unauthorized(new WishlistToggleResult
            {
                Success = false,
                Message = "Sign in to manage your wishlist."
            });
        }

        var result = await wishlistService.RemoveAsync(customerEmail, request.ProductId, cancellationToken);
        return Json(result);
    }
}
