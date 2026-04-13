namespace PharmacyPOS.Models.Wishlist;

public class WishlistListResult
{
    public bool Success { get; init; }

    public int Count { get; init; }

    public List<WishlistItemViewModel> Items { get; init; } = [];

    public string Message { get; init; } = string.Empty;
}
