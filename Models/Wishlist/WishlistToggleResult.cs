namespace PharmacyPOS.Models.Wishlist;

public class WishlistToggleResult
{
    public bool Success { get; init; }

    public bool IsInWishlist { get; init; }

    public int Count { get; init; }

    public string Message { get; init; } = string.Empty;
}
