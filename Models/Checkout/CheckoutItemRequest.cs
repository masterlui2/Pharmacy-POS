namespace PharmacyPOS.Models.Checkout;

public class CheckoutItemRequest
{
    public string ProductId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Brand { get; set; } = string.Empty;

    public string Image { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public decimal Tax { get; set; }

    public int Quantity { get; set; }

    public bool RequiresPrescription { get; set; }
}
