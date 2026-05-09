using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace PharmacyPOS.Models;

[Index(nameof(Email), IsUnique = true)]
public class Account
{
    public int Id { get; set; }

    [Required]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = "Customer";

    public string FirebaseUid { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<CustomerAddress> CustomerAddresses { get; set; } = [];

    public List<PharmacyOrder> Orders { get; set; } = [];

    public List<WishlistItem> WishlistItems { get; set; } = [];
}
