using System.ComponentModel.DataAnnotations;

namespace PharmacyPOS.Models;

public class PharmacyOrderItem
{
    public int Id { get; set; }

    public int PharmacyOrderId { get; set; }

    [Required]
    public string ProductId { get; set; } = string.Empty;

    [Required]
    public string ProductName { get; set; } = string.Empty;

    public string BrandName { get; set; } = string.Empty;

    public string ImageUrl { get; set; } = string.Empty;

    public decimal UnitPrice { get; set; }

    public decimal TaxAmount { get; set; }

    public int Quantity { get; set; }

    public bool RequiresPrescription { get; set; }

    public PharmacyOrder? PharmacyOrder { get; set; }
}
