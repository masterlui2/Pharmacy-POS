namespace PharmacyPOS.Models.Orders;

public class OrderItemViewModel
{
    public string ProductName { get; init; } = string.Empty;

    public string BrandName { get; init; } = string.Empty;

    public string ImageUrl { get; init; } = string.Empty;

    public int Quantity { get; init; }

    public decimal UnitPrice { get; init; }

    public bool RequiresPrescription { get; init; }
}
