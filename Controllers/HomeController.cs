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
            HeroSlides =
            [
                new()
                {
                    Eyebrow = "Main Advertisement",
                    Title = "Daily medicine deals and trusted pharmacy essentials.",
                    Description = "Shop trusted pharmacy picks, fast-moving wellness staples, and curated promotions in one streamlined storefront experience.",
                    ImagePath = "~/images/mainhero.png",
                    PrimaryActionLabel = "Shop Now",
                    SecondaryActionLabel = "View Offers"
                },
                new()
                {
                    Eyebrow = "Featured Promotion",
                    Title = "Seasonal care bundles and everyday essentials in one place.",
                    Description = "Browse fresh homepage offers built around the new hero artwork, with a cleaner layout that highlights promotions without crowding the product sections.",
                    ImagePath = "~/images/hero2.png",
                    PrimaryActionLabel = "Browse Deals",
                    SecondaryActionLabel = "Learn More"
                }
            ],
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
            PromoPanels =
            [
                new()
                {
                    Label = "Prescription",
                    TitlePrimary = "Prescription",
                    TitleAccent = "Medicines",
                    ImagePath = "~/images/prescript.png",
                    ActionLabel = "View more"
                },
                new()
                {
                    Label = "OTC",
                    TitlePrimary = "Over the Counter",
                    TitleAccent = "Medicines",
                    ImagePath = "~/images/otc.png",
                    ActionLabel = "View more"
                }
            ],
            FeaturedMedicines =
            [
                CreateFeaturedCard(
                    id: "coldzep",
                    imagePath: "~/images/m1.png",
                    categoryLabel: "Cold, sipon & trangkaso",
                    quantityLabel: "100",
                    brandName: "COLDZEP",
                    productName: "Paracetamol + Phenylephrine HCl + Chlorphenamine",
                    fallbackPrice: 498.75m,
                    requiresPrescription: true,
                    inventory),
                CreateFeaturedCard(
                    id: "coq10",
                    imagePath: "~/images/m2.png",
                    categoryLabel: "Food supplement",
                    quantityLabel: "10",
                    brandName: "COQ10",
                    productName: "Coenzyme Q10 (Ubiquinone) 30mg Softgel Capsule 10s",
                    fallbackPrice: 110.25m,
                    requiresPrescription: false,
                    inventory),
                CreateFeaturedCard(
                    id: "biogesic",
                    imagePath: "~/images/m3.png",
                    categoryLabel: "Sakit ng ulo, lagnat",
                    quantityLabel: "100",
                    brandName: "BIOGESIC",
                    productName: "Paracetamol 500mg Tablet 100s",
                    fallbackPrice: 183.75m,
                    requiresPrescription: false,
                    inventory),
                CreateFeaturedCard(
                    id: "diatabs",
                    imagePath: "~/images/m4.png",
                    categoryLabel: "Tiyan at digestive care",
                    quantityLabel: "24",
                    brandName: "DIATABS",
                    productName: "Loperamide capsule support for diarrhea relief",
                    fallbackPrice: 12.00m,
                    requiresPrescription: false,
                    inventory)
            ]
        };

        return View(vm);
    }

    public IActionResult Cart()
    {
        return View();
    }

    public IActionResult Wishlist()
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
        string id,
        string imagePath,
        string categoryLabel,
        string quantityLabel,
        string brandName,
        string productName,
        decimal fallbackPrice,
        bool requiresPrescription,
        IReadOnlyDictionary<string, Medicine> inventory)
    {
        inventory.TryGetValue(brandName, out var medicine);
        var price = medicine?.Price ?? fallbackPrice;
        var resolvedRequiresPrescription = medicine?.RequiresPrescription ?? requiresPrescription;
        var includedTax = Math.Round(price * 0.12m, 2);

        return new FeaturedMedicineCardViewModel
        {
            Id = id,
            ImagePath = imagePath,
            CategoryLabel = categoryLabel,
            QuantityLabel = quantityLabel,
            BrandName = brandName,
            ProductName = productName,
            PriceLabel = $"\u20B1{price:N2}",
            UnitPrice = price,
            IncludedTax = includedTax,
            RequiresPrescription = resolvedRequiresPrescription
        };
    }
}
