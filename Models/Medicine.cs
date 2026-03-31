using System.ComponentModel.DataAnnotations;

namespace PharmacyPOS.Models;

public class Medicine
{
    public int Id { get; set; }

    [Required, StringLength(20)]
    public string Code { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string BrandName { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string GenericName { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string Category { get; set; } = string.Empty;

    [Range(0.01, 100000)]
    public decimal Price { get; set; }

    [Range(0, int.MaxValue)]
    public int Stock { get; set; }

    [DataType(DataType.Date)]
    public DateTime ExpiryDate { get; set; }

    public string Status => Stock switch
    {
        <= 0 => "Out of Stock",
        <= 20 => "Low Stock",
        _ => "In Stock"
    };
}