namespace PharmacyPOS.Models;

public class HomeIndexViewModel
{
    public required HomeHeroViewModel Hero { get; set; }
    public List<HomeCategoryCardViewModel> Categories { get; set; } = [];
    public List<FeaturedMedicineCardViewModel> FeaturedMedicines { get; set; } = [];
}

public class HomeHeroViewModel
{
    public required string Eyebrow { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    public required string ImagePath { get; set; }
    public required string PrimaryActionLabel { get; set; }
    public required string SecondaryActionLabel { get; set; }
}

public class FeaturedMedicineCardViewModel
{
    public required string ImagePath { get; set; }
    public required string CategoryLabel { get; set; }
    public required string QuantityLabel { get; set; }
    public required string BrandName { get; set; }
    public required string ProductName { get; set; }
    public required string PriceLabel { get; set; }
}

public class HomeCategoryCardViewModel
{
    public required string ImagePath { get; set; }
    public required string Title { get; set; }
    public required string Subtitle { get; set; }
}
