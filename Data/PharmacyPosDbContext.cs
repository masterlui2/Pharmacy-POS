using Microsoft.EntityFrameworkCore;
using PharmacyPOS.Models;

namespace PharmacyPOS.Data;

public class PharmacyPosDbContext(DbContextOptions<PharmacyPosDbContext> options) : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<CustomerAddress> CustomerAddresses => Set<CustomerAddress>();
    public DbSet<PharmacyOrder> Orders => Set<PharmacyOrder>();
    public DbSet<PharmacyOrderItem> OrderItems => Set<PharmacyOrderItem>();
    public DbSet<PaymentRecord> Payments => Set<PaymentRecord>();
    public DbSet<WishlistItem> WishlistItems => Set<WishlistItem>();

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
            entity.Property(account => account.FirebaseUid).HasMaxLength(128);
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
            entity.Property(order => order.CustomerUid).HasMaxLength(128);
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
            entity.Property(order => order.SubtotalAmount).HasPrecision(18, 2);
            entity.Property(order => order.TaxAmount).HasPrecision(18, 2);
            entity.Property(order => order.ShippingAmount).HasPrecision(18, 2);
            entity.Property(order => order.DiscountAmount).HasPrecision(18, 2);
            entity.Property(order => order.TotalAmount).HasPrecision(18, 2);
        });

        modelBuilder.Entity<PharmacyOrderItem>(entity =>
        {
            entity.Property(item => item.ProductId).HasMaxLength(64);
            entity.Property(item => item.ProductName).HasMaxLength(200);
            entity.Property(item => item.BrandName).HasMaxLength(100);
            entity.Property(item => item.ImageUrl).HasMaxLength(500);
            entity.Property(item => item.UnitPrice).HasPrecision(18, 2);
            entity.Property(item => item.TaxAmount).HasPrecision(18, 2);
        });

        modelBuilder.Entity<PaymentRecord>(entity =>
        {
            entity.Property(payment => payment.PaymentMethod).HasMaxLength(32);
            entity.Property(payment => payment.Status).HasMaxLength(32);
            entity.Property(payment => payment.ReferenceNumber).HasMaxLength(64);
            entity.Property(payment => payment.Provider).HasMaxLength(32);
            entity.Property(payment => payment.ProviderCheckoutId).HasMaxLength(128);
            entity.Property(payment => payment.CheckoutUrl).HasMaxLength(1000);
            entity.Property(payment => payment.Amount).HasPrecision(18, 2);
            entity.HasOne(payment => payment.PharmacyOrder)
                .WithOne(order => order.Payment)
                .HasForeignKey<PaymentRecord>(payment => payment.PharmacyOrderId);
        });

        modelBuilder.Entity<WishlistItem>(entity =>
        {
            entity.Property(item => item.ProductId).HasMaxLength(64);
            entity.Property(item => item.ProductName).HasMaxLength(200);
            entity.Property(item => item.BrandName).HasMaxLength(100);
            entity.Property(item => item.ImageUrl).HasMaxLength(500);
            entity.Property(item => item.UnitPrice).HasPrecision(18, 2);
            entity.HasOne(item => item.Account)
                .WithMany(account => account.WishlistItems)
                .HasForeignKey(item => item.AccountId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
