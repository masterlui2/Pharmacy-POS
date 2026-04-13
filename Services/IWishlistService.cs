using PharmacyPOS.Models.Wishlist;

namespace PharmacyPOS.Services;

public interface IWishlistService
{
    Task<WishlistToggleResult> ToggleAsync(
        string customerEmail,
        WishlistToggleRequest request,
        CancellationToken cancellationToken = default);

    Task<WishlistListResult> GetItemsAsync(
        string customerEmail,
        CancellationToken cancellationToken = default);

    Task<WishlistToggleResult> RemoveAsync(
        string customerEmail,
        string productId,
        CancellationToken cancellationToken = default);
}
