using Microsoft.EntityFrameworkCore;
using PharmacyPOS.Data;
using PharmacyPOS.Models;
using PharmacyPOS.Models.Wishlist;

namespace PharmacyPOS.Services;

public class WishlistService(PharmacyPosDbContext dbContext) : IWishlistService
{
    public async Task<WishlistToggleResult> ToggleAsync(
        string customerEmail,
        WishlistToggleRequest request,
        CancellationToken cancellationToken = default)
    {
        var account = await dbContext.Accounts
            .FirstOrDefaultAsync(candidate => candidate.Email == customerEmail, cancellationToken);

        if (account is null)
        {
            return new WishlistToggleResult
            {
                Success = false,
                Message = "Sign in to save medicines to your wishlist."
            };
        }

        var existing = await dbContext.WishlistItems
            .FirstOrDefaultAsync(
                item => item.AccountId == account.Id && item.ProductId == request.ProductId,
                cancellationToken);

        var isInWishlist = false;
        if (existing is null)
        {
            dbContext.WishlistItems.Add(new WishlistItem
            {
                AccountId = account.Id,
                ProductId = request.ProductId.Trim(),
                ProductName = request.ProductName.Trim(),
                BrandName = request.BrandName.Trim(),
                ImageUrl = request.ImageUrl.Trim(),
                UnitPrice = request.UnitPrice,
                RequiresPrescription = request.RequiresPrescription,
                CreatedAtUtc = DateTime.UtcNow
            });
            isInWishlist = true;
        }
        else
        {
            dbContext.WishlistItems.Remove(existing);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        var count = await dbContext.WishlistItems.CountAsync(item => item.AccountId == account.Id, cancellationToken);

        return new WishlistToggleResult
        {
            Success = true,
            IsInWishlist = isInWishlist,
            Count = count,
            Message = isInWishlist ? "Added to wishlist." : "Removed from wishlist."
        };
    }

    public async Task<WishlistListResult> GetItemsAsync(
        string customerEmail,
        CancellationToken cancellationToken = default)
    {
        var account = await dbContext.Accounts
            .FirstOrDefaultAsync(candidate => candidate.Email == customerEmail, cancellationToken);

        if (account is null)
        {
            return new WishlistListResult
            {
                Success = false,
                Message = "Sign in to view your wishlist."
            };
        }

        var items = await dbContext.WishlistItems
            .Where(item => item.AccountId == account.Id)
            .OrderByDescending(item => item.CreatedAtUtc)
            .Select(item => new WishlistItemViewModel
            {
                ProductId = item.ProductId,
                ProductName = item.ProductName,
                BrandName = item.BrandName,
                ImageUrl = item.ImageUrl,
                UnitPrice = item.UnitPrice,
                RequiresPrescription = item.RequiresPrescription
            })
            .ToListAsync(cancellationToken);

        return new WishlistListResult
        {
            Success = true,
            Count = items.Count,
            Items = items
        };
    }

    public async Task<WishlistToggleResult> RemoveAsync(
        string customerEmail,
        string productId,
        CancellationToken cancellationToken = default)
    {
        var account = await dbContext.Accounts
            .FirstOrDefaultAsync(candidate => candidate.Email == customerEmail, cancellationToken);

        if (account is null)
        {
            return new WishlistToggleResult
            {
                Success = false,
                Message = "Sign in to manage your wishlist."
            };
        }

        var existing = await dbContext.WishlistItems
            .FirstOrDefaultAsync(
                item => item.AccountId == account.Id && item.ProductId == productId,
                cancellationToken);

        if (existing is not null)
        {
            dbContext.WishlistItems.Remove(existing);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var count = await dbContext.WishlistItems.CountAsync(item => item.AccountId == account.Id, cancellationToken);
        return new WishlistToggleResult
        {
            Success = true,
            IsInWishlist = false,
            Count = count,
            Message = "Removed from wishlist."
        };
    }
}
