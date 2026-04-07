using Microsoft.AspNetCore.Mvc;

namespace PharmacyPOS.Controllers;

public class DashboardController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
