using Microsoft.AspNetCore.Mvc;
using PharmacyPOS.Models;

namespace PharmacyPOS.Controllers;

public class AuthController : Controller
{
    [HttpGet]
    public IActionResult Login()
    {
        if (IsAuthenticated())
        {
            return RedirectToAction("Index", "Dashboard");
        }

        return View(new LoginViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (model.Username.Equals("admin", StringComparison.OrdinalIgnoreCase) && model.Password == "admin123")
        {
            HttpContext.Session.SetString("Username", "Admin");
            HttpContext.Session.SetString("Role", "Admin");
            return RedirectToAction("Index", "Dashboard");
        }

        ModelState.AddModelError(string.Empty, "Invalid username or password.");
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Login", "Auth");
    }

    private bool IsAuthenticated() => !string.IsNullOrWhiteSpace(HttpContext.Session.GetString("Username"));
}
