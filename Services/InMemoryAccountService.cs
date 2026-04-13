namespace PharmacyPOS.Services;

public class InMemoryAccountService : IAccountService
{
    private readonly List<AccountRecord> _accounts =
    [
        new()
        {
            FirstName = "Admin",
            LastName = "User",
            Email = "admin@safemed.local",
            PhoneNumber = "0000000000",
            Password = "admin123",
            Role = "Admin",
            LoginAliases = ["admin", "admin@safemed.local"]
        }
    ];

    private readonly object _syncRoot = new();

    public bool ValidateCredentials(string usernameOrEmail, string password, out AuthenticatedAccount? account)
    {
        lock (_syncRoot)
        {
            var match = _accounts.FirstOrDefault(candidate =>
                candidate.Password == password &&
                candidate.LoginAliases.Any(alias => alias.Equals(usernameOrEmail, StringComparison.OrdinalIgnoreCase)));

            if (match is null)
            {
                account = null;
                return false;
            }

            account = new AuthenticatedAccount
            {
                DisplayName = $"{match.FirstName} {match.LastName}".Trim(),
                Email = match.Email,
                PhoneNumber = match.PhoneNumber,
                Role = match.Role
            };

            return true;
        }
    }

    public bool EmailExists(string email)
    {
        lock (_syncRoot)
        {
            return _accounts.Any(account => account.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
        }
    }

    public AuthenticatedAccount Register(string firstName, string lastName, string email, string phoneNumber, string password)
    {
        var normalizedEmail = email.Trim();

        lock (_syncRoot)
        {
            var record = new AccountRecord
            {
                FirstName = firstName.Trim(),
                LastName = lastName.Trim(),
                Email = normalizedEmail,
                PhoneNumber = phoneNumber.Trim(),
                Password = password,
                Role = "Customer",
                LoginAliases = [normalizedEmail]
            };

            _accounts.Add(record);

            return new AuthenticatedAccount
            {
                DisplayName = $"{record.FirstName} {record.LastName}".Trim(),
                Email = record.Email,
                PhoneNumber = record.PhoneNumber,
                Role = record.Role
            };
        }
    }

    private sealed class AccountRecord
    {
        public required string FirstName { get; init; }
        public required string LastName { get; init; }
        public required string Email { get; init; }
        public required string PhoneNumber { get; init; }
        public required string Password { get; init; }
        public required string Role { get; init; }
        public required List<string> LoginAliases { get; init; }
    }
}
