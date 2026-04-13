using PharmacyPOS.Models;

namespace PharmacyPOS.Services;

public interface IRecaptchaService
{
    Task<RecaptchaVerificationResult> VerifyAsync(string token, string? remoteIpAddress, CancellationToken cancellationToken = default);
}
