using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PharmacyPOS.Models;

namespace PharmacyPOS.Controllers;

public class AuthController : Controller
{
    [HttpGet]
    public IActionResult Login()
    {
        if (!string.IsNullOrWhiteSpace(HttpContext.Session.GetString("Username")))
        {
            return RedirectToAction("Index", "Home");
        }

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (model.Username == "admin" && model.Password == "admin123")
        {
            HttpContext.Session.SetString("Username", model.Username);
            HttpContext.Session.SetString("Role", "Admin");

            return RedirectToAction("Index", "Home");
        }

        ViewBag.ErrorMessage = "Invalid username or password.";
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Login", "Auth");
    }
}