namespace PharmacyPOS.Models.Security;

public static class AppRoles
{
    public const string Admin = "Admin";
    public const string Pharmacist = "Pharmacist";
    public const string Customer = "Customer";

    public static bool IsBackOffice(string? role) =>
        string.Equals(role, Admin, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(role, Pharmacist, StringComparison.OrdinalIgnoreCase);

    public static bool Matches(string? role, string expectedRole) =>
        string.Equals(role, expectedRole, StringComparison.OrdinalIgnoreCase);
}
