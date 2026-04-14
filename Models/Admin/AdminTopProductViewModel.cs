namespace PharmacyPOS.Models.Admin;

public class AdminTopProductViewModel
{
    public string ProductName { get; init; } = string.Empty;
    public int QuantitySold { get; init; }
    public decimal Revenue { get; init; }
}
