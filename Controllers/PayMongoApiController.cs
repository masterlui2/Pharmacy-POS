using Microsoft.AspNetCore.Mvc;
using PharmacyPOS.Models.PayMongoApi;
using PharmacyPOS.Services;

namespace PharmacyPOS.Controllers;

[ApiController]
[Route("api/paymongo")]
public class PayMongoApiController(
    ICheckoutService checkoutService,
    ILogger<PayMongoApiController> logger) : ControllerBase
{
    [HttpPost("create-checkout-session")]
    [Consumes("application/json")]
    [Produces("application/json")]
    public async Task<IActionResult> CreateCheckoutSession(
        [FromBody] MobileCreateCheckoutSessionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await checkoutService.CreateMobileCheckoutSessionAsync(
                request,
                request.SuccessUrl,
                request.CancelUrl,
                cancellationToken);

            var response = new MobileCreateCheckoutSessionResponse
            {
                Success = result.Success,
                CheckoutUrl = result.CheckoutUrl,
                OrderNumber = result.OrderNumber,
                Message = result.Message
            };

            if (!result.Success)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to create mobile PayMongo checkout session.");
            return StatusCode(StatusCodes.Status500InternalServerError, new MobileCreateCheckoutSessionResponse
            {
                Success = false,
                Message = "An unexpected error occurred while creating the checkout session."
            });
        }
    }
}
