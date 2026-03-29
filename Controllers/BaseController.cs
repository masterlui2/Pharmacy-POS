using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace PharmacyPOS.Controllers;

public abstract class BaseController : Controller
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var username = HttpContext.Session.GetString("Username");

        if (string.IsNullOrWhiteSpace(username))
        {
            context.Result = RedirectToAction("Login", "Auth");
            return;
        }

        base.OnActionExecuting(context);
    }
}