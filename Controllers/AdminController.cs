using PharmacyPOS.Models.Security;

namespace PharmacyPOS.Controllers;

public abstract class AdminController : RoleProtectedController
{
    private static readonly IReadOnlyCollection<string> RoleSet = [AppRoles.Admin];

    protected override IReadOnlyCollection<string> AllowedRoles => RoleSet;
}
