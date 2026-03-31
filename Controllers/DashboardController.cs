using Microsoft.AspNetCore.Mvc;

namespace PharmacyPOS.Controllers;

public class DashboardController : Controller
{
    public IActionResult Index()
    {
        if (!IsAuthenticated())
        {
            return RedirectToAction("Login", "Auth");
        }

        return View();
    }

    private bool IsAuthenticated() => !string.IsNullOrWhiteSpace(HttpContext.Session.GetString("Username"));
}