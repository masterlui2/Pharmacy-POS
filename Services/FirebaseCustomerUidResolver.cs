using FirebaseAdmin.Auth;
using Microsoft.Extensions.Options;
using PharmacyPOS.Models;

namespace PharmacyPOS.Services;

public sealed class FirebaseCustomerUidResolver(
    FirebaseAppInitializer firebaseAppInitializer,
    IOptions<FirebaseOptions> firebaseOptionsAccessor,
    ILogger<FirebaseCustomerUidResolver> logger) : IFirebaseCustomerUidResolver
{
    private readonly FirebaseOptions firebaseOptions = firebaseOptionsAccessor.Value;

    public async Task<string> ResolveCustomerUidAsync(
        Account? account,
        string? customerEmail,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(account?.FirebaseUid))
        {
            return account.FirebaseUid.Trim();
        }

        if (string.IsNullOrWhiteSpace(customerEmail))
        {
            return string.Empty;
        }

        var normalizedEmail = customerEmail.Trim();
        if (firebaseOptions.CustomerUidByEmail.TryGetValue(normalizedEmail, out var configuredUid) &&
            !string.IsNullOrWhiteSpace(configuredUid))
        {
            return configuredUid.Trim();
        }

        if (!firebaseAppInitializer.IsAuthenticationAvailable)
        {
            return string.Empty;
        }

        try
        {
            var user = await FirebaseAuth.DefaultInstance.GetUserByEmailAsync(
                normalizedEmail,
                cancellationToken);
            return user.Uid;
        }
        catch (FirebaseAuthException exception)
        {
            logger.LogWarning(
                exception,
                "Could not resolve Firebase UID for customer email {CustomerEmail}.",
                normalizedEmail);
            return string.Empty;
        }
    }
}
