using FirebaseAdmin.Auth;
using Microsoft.AspNetCore.Mvc;
using PharmacyPOS.Models.PayMongoApi;
using PharmacyPOS.Services;

namespace PharmacyPOS.Controllers;

[ApiController]
[Route("api/paymongo")]
public class PayMongoApiController(
    ICheckoutService checkoutService,
    FirebaseAppInitializer firebaseAppInitializer,
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
            if (!firebaseAppInitializer.IsAuthenticationAvailable)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new MobileCreateCheckoutSessionResponse
                {
                    Success = false,
                    Message = firebaseAppInitializer.AuthenticationUnavailableReason ??
                        "Firebase authentication is not configured on the server."
                });
            }

            var authHeader = Request.Headers.Authorization.ToString();
            if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return Unauthorized(new MobileCreateCheckoutSessionResponse
                {
                    Success = false,
                    Message = "A valid Firebase bearer token is required."
                });
            }

            var idToken = authHeader["Bearer ".Length..].Trim();
            FirebaseToken decodedToken;
            try
            {
                decodedToken = await FirebaseAuth.DefaultInstance.VerifyIdTokenAsync(idToken);
            }
            catch (Exception exception) when (exception is FirebaseAuthException or ArgumentException)
            {
                logger.LogWarning(exception, "Rejected mobile checkout request because Firebase token verification failed.");
                return Unauthorized(new MobileCreateCheckoutSessionResponse
                {
                    Success = false,
                    Message = "The Firebase session is invalid or expired."
                });
            }
            catch (InvalidOperationException exception)
            {
                logger.LogError(exception, "Firebase authentication is unavailable while handling mobile checkout.");
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new MobileCreateCheckoutSessionResponse
                {
                    Success = false,
                    Message = firebaseAppInitializer.AuthenticationUnavailableReason ??
                        "Firebase authentication is not configured on the server."
                });
            }

            var firebaseUid = decodedToken.Uid;
            var verifiedCustomerEmail = decodedToken.Claims.TryGetValue("email", out var emailClaim)
                ? Convert.ToString(emailClaim)
                : null;

            var requestCustomerUid = request.ResolveCustomerUid();
            if (!string.IsNullOrWhiteSpace(requestCustomerUid) &&
                !string.Equals(requestCustomerUid, firebaseUid, StringComparison.Ordinal))
            {
                logger.LogWarning(
                    "Mobile checkout request UID mismatch. Token UID {TokenUid} did not match request UID {RequestUid}.",
                    firebaseUid,
                    requestCustomerUid);
            }

            var result = await checkoutService.CreateMobileCheckoutSessionAsync(
                request,
                firebaseUid,
                verifiedCustomerEmail,
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
