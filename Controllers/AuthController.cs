using Microsoft.AspNetCore.Mvc;
using PharmacyPOS.Models;
using PharmacyPOS.Models.Security;
using PharmacyPOS.Services;
using Microsoft.Extensions.Options;

namespace PharmacyPOS.Controllers;

public class AuthController(
    IAccountService accountService,
    IRecaptchaService recaptchaService,
    IOptions<RecaptchaOptions> recaptchaOptions) : Controller
{
    [HttpGet]
    public IActionResult Login()
    {
        if (IsAuthenticated())
        {
            return RedirectAuthenticatedUser();
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

        if (accountService.ValidateCredentials(model.Username, model.Password, out var account) && account is not null)
        {
            HttpContext.Session.SetString("Username", account.DisplayName);
            HttpContext.Session.SetString("Role", account.Role);
            HttpContext.Session.SetString("Email", account.Email);
            HttpContext.Session.SetString("PhoneNumber", account.PhoneNumber);

            return RedirectAuthenticatedUser(account.Role);
        }

        ModelState.AddModelError(string.Empty, "Invalid username or password.");
        return View(model);
    }

    [HttpGet]
    public IActionResult Register()
    {
        if (IsAuthenticated())
        {
            return RedirectAuthenticatedUser();
        }

        ViewBag.RecaptchaSiteKey = recaptchaOptions.Value.SiteKey;
        return View(new RegisterViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model, CancellationToken cancellationToken)
    {
        ViewBag.RecaptchaSiteKey = recaptchaOptions.Value.SiteKey;

        if (accountService.EmailExists(model.Email))
        {
            ModelState.AddModelError(nameof(model.Email), "An account with this email already exists.");
        }

        var recaptchaResult = await recaptchaService.VerifyAsync(
            model.RecaptchaToken,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken);

        if (!recaptchaResult.Success)
        {
            ModelState.AddModelError(nameof(model.RecaptchaToken), recaptchaResult.ErrorMessage);
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        accountService.Register(model.FirstName, model.LastName, model.Email, model.PhoneNumber, model.Password);
        TempData["AuthSuccessMessage"] = "Account created successfully. You can now log in.";

        return RedirectToAction(nameof(Login));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Index", "Home");
    }

    private bool IsAuthenticated() => !string.IsNullOrWhiteSpace(HttpContext.Session.GetString("Username"));

    private IActionResult RedirectAuthenticatedUser(string? role = null)
    {
        var resolvedRole = role ?? HttpContext.Session.GetString("Role");
        if (AppRoles.Matches(resolvedRole, AppRoles.Admin))
        {
            return RedirectToAction("Index", "Dashboard");
        }

        if (AppRoles.Matches(resolvedRole, AppRoles.Pharmacist))
        {
            return RedirectToAction("Prescriptions", "PharmacistModules");
        }

        return RedirectToAction("Index", "Home");
    }
}
