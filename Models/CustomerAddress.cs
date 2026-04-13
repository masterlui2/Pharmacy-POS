using System.ComponentModel.DataAnnotations;

namespace PharmacyPOS.Models;

public class CustomerAddress
{
    public int Id { get; set; }

    public int? AccountId { get; set; }

    [Required]
    public string FullName { get; set; } = string.Empty;

    [Required]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required]
    public string DeliveryAddress { get; set; } = string.Empty;

    public string Landmark { get; set; } = string.Empty;

    [Required]
    public string AddressType { get; set; } = "Home";

    public bool IsDefault { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public Account? Account { get; set; }
}
