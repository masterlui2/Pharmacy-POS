namespace PharmacyPOS.Models.Checkout;

public class GoogleMapsDeliveryOptions
{
    public const string SectionName = "GoogleMapsDelivery";

    public string ApiKey { get; set; } = string.Empty;

    public string MapId { get; set; } = string.Empty;

    public string BranchName { get; set; } = "SafeMed Davao Dispatch";

    public string BranchAddress { get; set; } = "Davao City, Davao del Sur, Philippines";

    public double BranchLatitude { get; set; } = 7.073056;

    public double BranchLongitude { get; set; } = 125.612778;

    public double BaseDistanceKm { get; set; } = 3;

    public double MaxRadiusKm { get; set; } = 18;

    public decimal BaseFee { get; set; } = 59m;

    public decimal PerKmFee { get; set; } = 12m;
}
