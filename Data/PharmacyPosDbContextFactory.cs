using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PharmacyPOS.Data;

public class PharmacyPosDbContextFactory : IDesignTimeDbContextFactory<PharmacyPosDbContext>
{
    public PharmacyPosDbContext CreateDbContext(string[] args)
    {
        var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{environmentName}.json", optional: true)
            .AddJsonFile("appsettings.Local.json", optional: true)
            .AddJsonFile($"appsettings.{environmentName}.local.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("The DefaultConnection connection string is not configured.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<PharmacyPosDbContext>();
        optionsBuilder.UseSqlServer(connectionString);
        return new PharmacyPosDbContext(optionsBuilder.Options);
    }
}
