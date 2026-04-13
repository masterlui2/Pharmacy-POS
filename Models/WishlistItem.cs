using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace PharmacyPOS.Models;

[Index(nameof(AccountId), nameof(ProductId), IsUnique = true)]
public class WishlistItem
{
    public int Id { get; set; }

    public int AccountId { get; set; }

    [Required]
    public string ProductId { get; set; } = string.Empty;

    [Required]
    public string ProductName { get; set; } = string.Empty;

    [Required]
    public string BrandName { get; set; } = string.Empty;

    [Required]
    public string ImageUrl { get; set; } = string.Empty;

    public decimal UnitPrice { get; set; }

    public bool RequiresPrescription { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public Account? Account { get; set; }
}
