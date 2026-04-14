using Microsoft.AspNetCore.Mvc;
using PharmacyPOS.Models.Admin;

namespace PharmacyPOS.Controllers;

public class ModulesController : BaseController
{
    public IActionResult Pos() => GuardedView("POS / Sales");
    public IActionResult Payment() => GuardedView("Payment");
    public IActionResult Receipt() => GuardedView("Receipt");
    public IActionResult SalesHistory() => GuardedView("Sales History");
    public IActionResult Reports() => GuardedView("Reports");
    public IActionResult StockAlerts() => GuardedView("Stock Alerts");

    private IActionResult GuardedView(string moduleName)
    {
        var vm = new AdminModuleViewModel
        {
            Title = moduleName,
            Description = $"{moduleName} stays inside the admin shell and is ready for a dedicated implementation.",
            Checklist =
            [
                "Admin sidebar and active state remain visible while navigating.",
                "This page now uses the admin layout instead of the storefront shell.",
                "You can expand this module without mixing dashboard, layout, and feature logic in one file."
            ]
        };

        return View("ModulePlaceholder", vm);
    }
}
