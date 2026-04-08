using Microsoft.AspNetCore.Mvc;

namespace PharmacyPOS.Controllers;

public class DashboardController : BaseController
{
    public IActionResult Index()
    {
        return View();
    }
}
