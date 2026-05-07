namespace PharmacyPOS.Models;

public class FirebaseOptions
{
    public const string SectionName = "Firebase";

    public string ProjectId { get; set; } = string.Empty;

    public string ServiceAccountPath { get; set; } = string.Empty;

    public string PharmacyName { get; set; } = string.Empty;

    public string DefaultPharmacistUid { get; set; } = string.Empty;

    public string DefaultPharmacistName { get; set; } = string.Empty;
}
