using PharmacyPOS.Models.Security;

namespace PharmacyPOS.Controllers;

public abstract class PharmacistOnlyController : RoleProtectedController
{
    private static readonly IReadOnlyCollection<string> RoleSet = [AppRoles.Pharmacist];

    protected override IReadOnlyCollection<string> AllowedRoles => RoleSet;
}
