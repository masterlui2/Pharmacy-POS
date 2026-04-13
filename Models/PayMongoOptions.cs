namespace PharmacyPOS.Models;

public class PayMongoOptions
{
    public const string SectionName = "PayMongo";

    public bool Enabled { get; set; }

    public string PublicKey { get; set; } = string.Empty;

    public string SecretKey { get; set; } = string.Empty;

    public string SuccessUrl { get; set; } = string.Empty;

    public string CancelUrl { get; set; } = string.Empty;
}
