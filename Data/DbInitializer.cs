using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PharmacyPOS.Models;

namespace PharmacyPOS.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        var dbContext = serviceProvider.GetRequiredService<PharmacyPosDbContext>();

        if (await dbContext.Accounts.AnyAsync(account => account.Email == "admin@safemed.local"))
        {
            return;
        }

        var admin = new Account
        {
            FirstName = "Admin",
            LastName = "User",
            Email = "admin@safemed.local",
            PhoneNumber = "0000000000",
            Role = "Admin",
            CreatedAtUtc = DateTime.UtcNow
        };

        var passwordHasher = new PasswordHasher<Account>();
        admin.PasswordHash = passwordHasher.HashPassword(admin, "admin123");

        dbContext.Accounts.Add(admin);
        await dbContext.SaveChangesAsync();
    }
}
