using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PharmacyPOS.Models;
using PharmacyPOS.Services;

namespace PharmacyPOS.Controllers;

public class HomeController(IMedicineService medicineService) : Controller
{
    public IActionResult Index()
    {
        var inventory = medicineService.GetAll().ToDictionary(m => m.BrandName, StringComparer.OrdinalIgnoreCase);

        var vm = new HomeIndexViewModel
        {
            Hero = new HomeHeroViewModel
            {
                Eyebrow = "Main Advertisement",
                Title = "Daily medicine deals and trusted pharmacy essentials.",
                Description = "The homepage hero now uses the provided artwork to anchor promotions while keeping the storefront layout consistent with the rest of the site.",
                ImagePath = "~/images/hero.png",
                PrimaryActionLabel = "Shop Now",
                SecondaryActionLabel = "View Offers"
            },
            Categories =
            [
                new()
                {
                    ImagePath = "~/images/Categories/vit.png",
                    Title = "Vitamins &",
                    Subtitle = "Supplements"
                },
                new()
                {
                    ImagePath = "~/images/Categories/Remedies.png",
                    Title = "Home",
                    Subtitle = "Remedies"
                },
                new()
                {
                    ImagePath = "~/images/Categories/care.png",
                    Title = "Beauty & Personal",
                    Subtitle = "Care"
                },
                new()
                {
                    ImagePath = "~/images/Categories/devices.png",
                    Title = "Medical",
                    Subtitle = "Devices"
                }
            ],
            FeaturedMedicines =
            [
                CreateFeaturedCard(
                    imagePath: "~/images/m1.png",
                    categoryLabel: "Cold, sipon & trangkaso",
                    quantityLabel: "100",
                    brandName: "COLDZEP",
                    productName: "Paracetamol + Phenylephrine HCl + Chlorphenamine",
                    fallbackPrice: 498.75m,
                    inventory),
                CreateFeaturedCard(
                    imagePath: "~/images/m2.png",
                    categoryLabel: "Food supplement",
                    quantityLabel: "10",
                    brandName: "COQ10",
                    productName: "Coenzyme Q10 (Ubiquinone) 30mg Softgel Capsule 10s",
                    fallbackPrice: 110.25m,
                    inventory),
                CreateFeaturedCard(
                    imagePath: "~/images/m3.png",
                    categoryLabel: "Sakit ng ulo, lagnat",
                    quantityLabel: "100",
                    brandName: "BIOGESIC",
                    productName: "Paracetamol 500mg Tablet 100s",
                    fallbackPrice: 183.75m,
                    inventory)
            ]
        };

        return View(vm);
    }

    public IActionResult Cart()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    private static FeaturedMedicineCardViewModel CreateFeaturedCard(
        string imagePath,
        string categoryLabel,
        string quantityLabel,
        string brandName,
        string productName,
        decimal fallbackPrice,
        IReadOnlyDictionary<string, Medicine> inventory)
    {
        var price = inventory.TryGetValue(brandName, out var medicine)
            ? medicine.Price
            : fallbackPrice;

        return new FeaturedMedicineCardViewModel
        {
            ImagePath = imagePath,
            CategoryLabel = categoryLabel,
            QuantityLabel = quantityLabel,
            BrandName = brandName,
            ProductName = productName,
            PriceLabel = $"\u20B1{price:N2}"
        };
    }
}
