using PharmacyPOS.Models;

namespace PharmacyPOS.Services;

public interface IFirebaseCustomerUidResolver
{
    Task<string> ResolveCustomerUidAsync(
        Account? account,
        string? customerEmail,
        CancellationToken cancellationToken = default);
}
