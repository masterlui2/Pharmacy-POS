namespace PharmacyPOS.Models;

public sealed class RecaptchaVerificationResult
{
    public bool Success { get; init; }

    public string ErrorMessage { get; init; } = string.Empty;
}
