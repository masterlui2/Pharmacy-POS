namespace PharmacyPOS.Models;

public class RecaptchaOptions
{
    public const string SectionName = "GoogleRecaptcha";

    public string SiteKey { get; set; } = string.Empty;

    public string SecretKey { get; set; } = string.Empty;
}
