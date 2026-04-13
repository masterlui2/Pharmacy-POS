namespace PharmacyPOS.Models.Wishlist;

public class WishlistItemViewModel
{
    public string ProductId { get; init; } = string.Empty;

    public string ProductName { get; init; } = string.Empty;

    public string BrandName { get; init; } = string.Empty;

    public string ImageUrl { get; init; } = string.Empty;

    public decimal UnitPrice { get; init; }

    public bool RequiresPrescription { get; init; }
}
