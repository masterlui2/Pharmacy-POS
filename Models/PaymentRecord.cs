using System.ComponentModel.DataAnnotations;

namespace PharmacyPOS.Models;

public class PaymentRecord
{
    public int Id { get; set; }

    public int PharmacyOrderId { get; set; }

    [Required]
    public string PaymentMethod { get; set; } = string.Empty;

    [Required]
    public string Status { get; set; } = "Pending";

    public decimal Amount { get; set; }

    public string ReferenceNumber { get; set; } = string.Empty;

    public string Provider { get; set; } = string.Empty;

    public string ProviderCheckoutId { get; set; } = string.Empty;

    public string CheckoutUrl { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public PharmacyOrder? PharmacyOrder { get; set; }
}
