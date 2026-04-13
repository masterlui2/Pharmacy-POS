using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PharmacyPOS.Data;
using PharmacyPOS.Models;

namespace PharmacyPOS.Services;

public class DatabaseAccountService(PharmacyPosDbContext dbContext) : IAccountService
{
    private readonly PasswordHasher<Account> _passwordHasher = new();

    public bool ValidateCredentials(string usernameOrEmail, string password, out AuthenticatedAccount? account)
    {
        var normalizedLogin = usernameOrEmail.Trim();
        var existingAccount = dbContext.Accounts
            .AsEnumerable()
            .FirstOrDefault(candidate =>
                string.Equals(candidate.Email, normalizedLogin, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(candidate.Email.Split('@')[0], normalizedLogin, StringComparison.OrdinalIgnoreCase));

        if (existingAccount is null)
        {
            account = null;
            return false;
        }

        var result = _passwordHasher.VerifyHashedPassword(existingAccount, existingAccount.PasswordHash, password);
        if (result == PasswordVerificationResult.Failed)
        {
            account = null;
            return false;
        }

        account = new AuthenticatedAccount
        {
            DisplayName = $"{existingAccount.FirstName} {existingAccount.LastName}".Trim(),
            Email = existingAccount.Email,
            PhoneNumber = existingAccount.PhoneNumber,
            Role = existingAccount.Role
        };

        return true;
    }

    public bool EmailExists(string email)
    {
        var normalizedEmail = email.Trim();
        return dbContext.Accounts.Any(account => account.Email == normalizedEmail);
    }

    public AuthenticatedAccount Register(string firstName, string lastName, string email, string phoneNumber, string password)
    {
        var account = new Account
        {
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            Email = email.Trim(),
            PhoneNumber = phoneNumber.Trim(),
            Role = "Customer",
            CreatedAtUtc = DateTime.UtcNow
        };

        account.PasswordHash = _passwordHasher.HashPassword(account, password);

        dbContext.Accounts.Add(account);
        dbContext.SaveChanges();

        return new AuthenticatedAccount
        {
            DisplayName = $"{account.FirstName} {account.LastName}".Trim(),
            Email = account.Email,
            PhoneNumber = account.PhoneNumber,
            Role = account.Role
        };
    }
}
