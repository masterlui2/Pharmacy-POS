namespace PharmacyPOS.Models.Checkout;

public class PlaceOrderRequest
{
    public string FullName { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    public string DeliveryAddress { get; set; } = string.Empty;

    public string Landmark { get; set; } = string.Empty;

    public string AddressType { get; set; } = "Home";

    public bool SaveAddress { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public double? DistanceKm { get; set; }

    public string DeliveryOption { get; set; } = "Standard";

    public string PaymentMethod { get; set; } = "CashOnDelivery";

    public string PrescriptionStatus { get; set; } = "Missing";

    public string PromoCode { get; set; } = string.Empty;

    public List<string> PrescriptionFiles { get; set; } = [];

    public List<CheckoutItemRequest> Items { get; set; } = [];
}
