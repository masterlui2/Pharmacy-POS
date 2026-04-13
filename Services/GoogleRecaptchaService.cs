using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using PharmacyPOS.Models;

namespace PharmacyPOS.Services;

public class GoogleRecaptchaService(
    HttpClient httpClient,
    IOptions<RecaptchaOptions> recaptchaOptions) : IRecaptchaService
{
    private readonly RecaptchaOptions _recaptchaOptions = recaptchaOptions.Value;

    public async Task<RecaptchaVerificationResult> VerifyAsync(string token, string? remoteIpAddress, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_recaptchaOptions.SecretKey))
        {
            return new RecaptchaVerificationResult
            {
                Success = false,
                ErrorMessage = "Google reCAPTCHA is not configured yet."
            };
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return new RecaptchaVerificationResult
            {
                Success = false,
                ErrorMessage = "Please complete the reCAPTCHA challenge."
            };
        }

        var payload = new Dictionary<string, string>
        {
            ["secret"] = _recaptchaOptions.SecretKey,
            ["response"] = token
        };

        if (!string.IsNullOrWhiteSpace(remoteIpAddress))
        {
            payload["remoteip"] = remoteIpAddress;
        }

        using var response = await httpClient.PostAsync(
            "https://www.google.com/recaptcha/api/siteverify",
            new FormUrlEncodedContent(payload),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return new RecaptchaVerificationResult
            {
                Success = false,
                ErrorMessage = "Unable to verify reCAPTCHA right now."
            };
        }

        var verification = await response.Content.ReadFromJsonAsync<GoogleRecaptchaVerifyResponse>(cancellationToken: cancellationToken);
        if (verification?.Success == true)
        {
            return new RecaptchaVerificationResult { Success = true };
        }

        return new RecaptchaVerificationResult
        {
            Success = false,
            ErrorMessage = "reCAPTCHA verification failed. Please try again."
        };
    }

    private sealed class GoogleRecaptchaVerifyResponse
    {
        public bool Success { get; set; }
    }
}
