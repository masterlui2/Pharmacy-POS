namespace PharmacyPOS.Models.Wishlist;

public class WishlistToggleRequest
{
    public string ProductId { get; set; } = string.Empty;

    public string ProductName { get; set; } = string.Empty;

    public string BrandName { get; set; } = string.Empty;

    public string ImageUrl { get; set; } = string.Empty;

    public decimal UnitPrice { get; set; }

    public bool RequiresPrescription { get; set; }
}
