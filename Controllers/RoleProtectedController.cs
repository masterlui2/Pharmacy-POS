using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PharmacyPOS.Models.Security;

namespace PharmacyPOS.Controllers;

public abstract class RoleProtectedController : BaseController
{
    protected abstract IReadOnlyCollection<string> AllowedRoles { get; }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        base.OnActionExecuting(context);

        if (context.Result is not null)
        {
            return;
        }

        var role = HttpContext.Session.GetString("Role");
        if (AllowedRoles.Any(candidate => AppRoles.Matches(role, candidate)))
        {
            return;
        }

        context.Result = ResolveRoleRedirect(role);
    }

    protected string CurrentRole => HttpContext.Session.GetString("Role") ?? string.Empty;

    protected string CurrentUsername => HttpContext.Session.GetString("Username") ?? "Staff";

    protected IActionResult ResolveRoleRedirect(string? role) =>
        AppRoles.Matches(role, AppRoles.Admin)
            ? RedirectToAction("Index", "Dashboard")
            : AppRoles.Matches(role, AppRoles.Pharmacist)
                ? RedirectToAction("Prescriptions", "PharmacistModules")
                : RedirectToAction("Index", "Home");
}
