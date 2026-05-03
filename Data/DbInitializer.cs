using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PharmacyPOS.Models;
using PharmacyPOS.Models.Security;

namespace PharmacyPOS.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        var dbContext = serviceProvider.GetRequiredService<PharmacyPosDbContext>();

        if (await dbContext.Accounts.AnyAsync(account => account.Email == "admin@safemed.local"))
        {
            if (!await dbContext.Accounts.AnyAsync(account => account.Email == "pharmacist@safemed.local"))
            {
                var pharmacist = CreateAccount(
                    "Pharmacist",
                    "User",
                    "pharmacist@safemed.local",
                    "09170000000",
                    AppRoles.Pharmacist,
                    "pharma123");

                dbContext.Accounts.Add(pharmacist);
                await dbContext.SaveChangesAsync();
            }

            return;
        }

        var admin = CreateAccount(
            "Admin",
            "User",
            "admin@safemed.local",
            "0000000000",
            AppRoles.Admin,
            "admin123");
        var pharmacistAccount = CreateAccount(
            "Pharmacist",
            "User",
            "pharmacist@safemed.local",
            "09170000000",
            AppRoles.Pharmacist,
            "pharma123");

        dbContext.Accounts.Add(admin);
        dbContext.Accounts.Add(pharmacistAccount);
        await dbContext.SaveChangesAsync();
    }

    private static Account CreateAccount(
        string firstName,
        string lastName,
        string email,
        string phoneNumber,
        string role,
        string password)
    {
        var account = new Account
        {
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            PhoneNumber = phoneNumber,
            Role = role,
            CreatedAtUtc = DateTime.UtcNow
        };

        var passwordHasher = new PasswordHasher<Account>();
        account.PasswordHash = passwordHasher.HashPassword(account, password);
        return account;
    }
}
