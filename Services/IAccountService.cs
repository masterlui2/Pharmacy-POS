namespace PharmacyPOS.Services;

public interface IAccountService
{
    bool ValidateCredentials(string usernameOrEmail, string password, out AuthenticatedAccount? account);
    bool EmailExists(string email);
    AuthenticatedAccount Register(string firstName, string lastName, string email, string phoneNumber, string password);
}

public sealed class AuthenticatedAccount
{
    public required string DisplayName { get; init; }
    public required string Email { get; init; }
    public required string PhoneNumber { get; init; }
    public required string Role { get; init; }
}
