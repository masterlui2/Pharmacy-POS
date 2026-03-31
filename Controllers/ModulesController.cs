using Microsoft.AspNetCore.Mvc;

namespace PharmacyPOS.Controllers;

public class ModulesController : Controller
{
    public IActionResult Pos() => GuardedView("POS / Sales");
    public IActionResult Payment() => GuardedView("Payment");
    public IActionResult Receipt() => GuardedView("Receipt");
    public IActionResult SalesHistory() => GuardedView("Sales History");
    public IActionResult Reports() => GuardedView("Reports");
    public IActionResult StockAlerts() => GuardedView("Stock Alerts");

    private IActionResult GuardedView(string moduleName)
    {
        if (string.IsNullOrWhiteSpace(HttpContext.Session.GetString("Username")))
        {
            return RedirectToAction("Login", "Auth");
        }

        ViewData["ModuleName"] = moduleName;
        return View("ModulePlaceholder");
    }
}