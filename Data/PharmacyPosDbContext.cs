using Microsoft.EntityFrameworkCore;
using PharmacyPOS.Models;

namespace PharmacyPOS.Data;

public class PharmacyPosDbContext(DbContextOptions<PharmacyPosDbContext> options) : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<CustomerAddress> CustomerAddresses => Set<CustomerAddress>();
    public DbSet<PharmacyOrder> Orders => Set<PharmacyOrder>();
    public DbSet<PharmacyOrderItem> OrderItems => Set<PharmacyOrderItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Account>(entity =>
        {
            entity.Property(account => account.FirstName).HasMaxLength(100);
            entity.Property(account => account.LastName).HasMaxLength(100);
            entity.Property(account => account.Email).HasMaxLength(256);
            entity.Property(account => account.PhoneNumber).HasMaxLength(32);
            entity.Property(account => account.PasswordHash).HasMaxLength(512);
            entity.Property(account => account.Role).HasMaxLength(32);
        });

        modelBuilder.Entity<CustomerAddress>(entity =>
        {
            entity.Property(address => address.FullName).HasMaxLength(150);
            entity.Property(address => address.PhoneNumber).HasMaxLength(32);
            entity.Property(address => address.DeliveryAddress).HasMaxLength(500);
            entity.Property(address => address.Landmark).HasMaxLength(250);
            entity.Property(address => address.AddressType).HasMaxLength(32);
        });

        modelBuilder.Entity<PharmacyOrder>(entity =>
        {
            entity.Property(order => order.OrderNumber).HasMaxLength(40);
            entity.Property(order => order.CustomerFullName).HasMaxLength(150);
            entity.Property(order => order.CustomerEmail).HasMaxLength(256);
            entity.Property(order => order.CustomerPhoneNumber).HasMaxLength(32);
            entity.Property(order => order.DeliveryAddress).HasMaxLength(500);
            entity.Property(order => order.Landmark).HasMaxLength(250);
            entity.Property(order => order.AddressType).HasMaxLength(32);
            entity.Property(order => order.DeliveryOption).HasMaxLength(32);
            entity.Property(order => order.PaymentMethod).HasMaxLength(32);
            entity.Property(order => order.FulfillmentBranch).HasMaxLength(160);
            entity.Property(order => order.PrescriptionStatus).HasMaxLength(32);
            entity.Property(order => order.OrderStatus).HasMaxLength(32);
            entity.Property(order => order.PromoCode).HasMaxLength(32);
            entity.Property(order => order.PrescriptionFilesJson).HasMaxLength(4000);
        });

        modelBuilder.Entity<PharmacyOrderItem>(entity =>
        {
            entity.Property(item => item.ProductId).HasMaxLength(64);
            entity.Property(item => item.ProductName).HasMaxLength(200);
            entity.Property(item => item.BrandName).HasMaxLength(100);
            entity.Property(item => item.ImageUrl).HasMaxLength(500);
        });
    }
}
