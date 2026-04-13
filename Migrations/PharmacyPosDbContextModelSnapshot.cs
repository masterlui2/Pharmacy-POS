using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using PharmacyPOS.Data;

#nullable disable

namespace PharmacyPOS.Migrations
{
    [DbContextAttribute(typeof(PharmacyPosDbContext))]
    partial class PharmacyPosDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "10.0.5")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

            modelBuilder.Entity("PharmacyPOS.Models.Account", b =>
                {
                    b.Property<int>("Id").ValueGeneratedOnAdd().HasColumnType("int");
                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"));
                    b.Property<DateTime>("CreatedAtUtc").HasColumnType("datetime2");
                    b.Property<string>("Email").IsRequired().HasMaxLength(256).HasColumnType("nvarchar(256)");
                    b.Property<string>("FirstName").IsRequired().HasMaxLength(100).HasColumnType("nvarchar(100)");
                    b.Property<string>("LastName").IsRequired().HasMaxLength(100).HasColumnType("nvarchar(100)");
                    b.Property<string>("PasswordHash").IsRequired().HasMaxLength(512).HasColumnType("nvarchar(512)");
                    b.Property<string>("PhoneNumber").IsRequired().HasMaxLength(32).HasColumnType("nvarchar(32)");
                    b.Property<string>("Role").IsRequired().HasMaxLength(32).HasColumnType("nvarchar(32)");
                    b.HasKey("Id");
                    b.HasIndex("Email").IsUnique();
                    b.ToTable("Accounts");
                });

            modelBuilder.Entity("PharmacyPOS.Models.CustomerAddress", b =>
                {
                    b.Property<int>("Id").ValueGeneratedOnAdd().HasColumnType("int");
                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"));
                    b.Property<int?>("AccountId").HasColumnType("int");
                    b.Property<string>("AddressType").IsRequired().HasMaxLength(32).HasColumnType("nvarchar(32)");
                    b.Property<DateTime>("CreatedAtUtc").HasColumnType("datetime2");
                    b.Property<string>("DeliveryAddress").IsRequired().HasMaxLength(500).HasColumnType("nvarchar(500)");
                    b.Property<string>("FullName").IsRequired().HasMaxLength(150).HasColumnType("nvarchar(150)");
                    b.Property<bool>("IsDefault").HasColumnType("bit");
                    b.Property<string>("Landmark").IsRequired().HasMaxLength(250).HasColumnType("nvarchar(250)");
                    b.Property<string>("PhoneNumber").IsRequired().HasMaxLength(32).HasColumnType("nvarchar(32)");
                    b.HasKey("Id");
                    b.HasIndex("AccountId");
                    b.ToTable("CustomerAddresses");
                });

            modelBuilder.Entity("PharmacyPOS.Models.PharmacyOrder", b =>
                {
                    b.Property<int>("Id").ValueGeneratedOnAdd().HasColumnType("int");
                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"));
                    b.Property<int?>("AccountId").HasColumnType("int");
                    b.Property<string>("AddressType").IsRequired().HasMaxLength(32).HasColumnType("nvarchar(32)");
                    b.Property<DateTime>("CreatedAtUtc").HasColumnType("datetime2");
                    b.Property<string>("CustomerEmail").IsRequired().HasMaxLength(256).HasColumnType("nvarchar(256)");
                    b.Property<string>("CustomerFullName").IsRequired().HasMaxLength(150).HasColumnType("nvarchar(150)");
                    b.Property<string>("CustomerPhoneNumber").IsRequired().HasMaxLength(32).HasColumnType("nvarchar(32)");
                    b.Property<string>("DeliveryAddress").IsRequired().HasMaxLength(500).HasColumnType("nvarchar(500)");
                    b.Property<string>("DeliveryOption").IsRequired().HasMaxLength(32).HasColumnType("nvarchar(32)");
                    b.Property<decimal>("DiscountAmount").HasColumnType("decimal(18,2)");
                    b.Property<int>("EstimatedDeliveryMaxMinutes").HasColumnType("int");
                    b.Property<int>("EstimatedDeliveryMinMinutes").HasColumnType("int");
                    b.Property<string>("FulfillmentBranch").IsRequired().HasMaxLength(160).HasColumnType("nvarchar(160)");
                    b.Property<string>("Landmark").IsRequired().HasMaxLength(250).HasColumnType("nvarchar(250)");
                    b.Property<string>("OrderNumber").IsRequired().HasMaxLength(40).HasColumnType("nvarchar(40)");
                    b.Property<string>("OrderStatus").IsRequired().HasMaxLength(32).HasColumnType("nvarchar(32)");
                    b.Property<string>("PaymentMethod").IsRequired().HasMaxLength(32).HasColumnType("nvarchar(32)");
                    b.Property<string>("PrescriptionFilesJson").IsRequired().HasMaxLength(4000).HasColumnType("nvarchar(4000)");
                    b.Property<string>("PrescriptionStatus").IsRequired().HasMaxLength(32).HasColumnType("nvarchar(32)");
                    b.Property<string>("PromoCode").IsRequired().HasMaxLength(32).HasColumnType("nvarchar(32)");
                    b.Property<bool>("RequiresPrescription").HasColumnType("bit");
                    b.Property<decimal>("ShippingAmount").HasColumnType("decimal(18,2)");
                    b.Property<decimal>("SubtotalAmount").HasColumnType("decimal(18,2)");
                    b.Property<decimal>("TaxAmount").HasColumnType("decimal(18,2)");
                    b.Property<decimal>("TotalAmount").HasColumnType("decimal(18,2)");
                    b.HasKey("Id");
                    b.HasIndex("AccountId");
                    b.ToTable("Orders");
                });

            modelBuilder.Entity("PharmacyPOS.Models.PharmacyOrderItem", b =>
                {
                    b.Property<int>("Id").ValueGeneratedOnAdd().HasColumnType("int");
                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"));
                    b.Property<string>("BrandName").IsRequired().HasMaxLength(100).HasColumnType("nvarchar(100)");
                    b.Property<string>("ImageUrl").IsRequired().HasMaxLength(500).HasColumnType("nvarchar(500)");
                    b.Property<int>("PharmacyOrderId").HasColumnType("int");
                    b.Property<string>("ProductId").IsRequired().HasMaxLength(64).HasColumnType("nvarchar(64)");
                    b.Property<string>("ProductName").IsRequired().HasMaxLength(200).HasColumnType("nvarchar(200)");
                    b.Property<int>("Quantity").HasColumnType("int");
                    b.Property<bool>("RequiresPrescription").HasColumnType("bit");
                    b.Property<decimal>("TaxAmount").HasColumnType("decimal(18,2)");
                    b.Property<decimal>("UnitPrice").HasColumnType("decimal(18,2)");
                    b.HasKey("Id");
                    b.HasIndex("PharmacyOrderId");
                    b.ToTable("OrderItems");
                });

            modelBuilder.Entity("PharmacyPOS.Models.PaymentRecord", b =>
                {
                    b.Property<int>("Id").ValueGeneratedOnAdd().HasColumnType("int");
                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"));
                    b.Property<decimal>("Amount").HasColumnType("decimal(18,2)");
                    b.Property<DateTime>("CreatedAtUtc").HasColumnType("datetime2");
                    b.Property<int>("PharmacyOrderId").HasColumnType("int");
                    b.Property<string>("PaymentMethod").IsRequired().HasMaxLength(32).HasColumnType("nvarchar(32)");
                    b.Property<string>("CheckoutUrl").IsRequired().HasMaxLength(1000).HasColumnType("nvarchar(1000)");
                    b.Property<string>("Provider").IsRequired().HasMaxLength(32).HasColumnType("nvarchar(32)");
                    b.Property<string>("ProviderCheckoutId").IsRequired().HasMaxLength(128).HasColumnType("nvarchar(128)");
                    b.Property<string>("ReferenceNumber").IsRequired().HasMaxLength(64).HasColumnType("nvarchar(64)");
                    b.Property<string>("Status").IsRequired().HasMaxLength(32).HasColumnType("nvarchar(32)");
                    b.HasKey("Id");
                    b.HasIndex("PharmacyOrderId").IsUnique();
                    b.ToTable("Payments");
                });

            modelBuilder.Entity("PharmacyPOS.Models.WishlistItem", b =>
                {
                    b.Property<int>("Id").ValueGeneratedOnAdd().HasColumnType("int");
                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"));
                    b.Property<int>("AccountId").HasColumnType("int");
                    b.Property<string>("BrandName").IsRequired().HasMaxLength(100).HasColumnType("nvarchar(100)");
                    b.Property<DateTime>("CreatedAtUtc").HasColumnType("datetime2");
                    b.Property<string>("ImageUrl").IsRequired().HasMaxLength(500).HasColumnType("nvarchar(500)");
                    b.Property<string>("ProductId").IsRequired().HasMaxLength(64).HasColumnType("nvarchar(64)");
                    b.Property<string>("ProductName").IsRequired().HasMaxLength(200).HasColumnType("nvarchar(200)");
                    b.Property<bool>("RequiresPrescription").HasColumnType("bit");
                    b.Property<decimal>("UnitPrice").HasColumnType("decimal(18,2)");
                    b.HasKey("Id");
                    b.HasIndex("AccountId", "ProductId").IsUnique();
                    b.ToTable("WishlistItems");
                });

            modelBuilder.Entity("PharmacyPOS.Models.CustomerAddress", b =>
                {
                    b.HasOne("PharmacyPOS.Models.Account", "Account")
                        .WithMany("CustomerAddresses")
                        .HasForeignKey("AccountId");
                });

            modelBuilder.Entity("PharmacyPOS.Models.WishlistItem", b =>
                {
                    b.HasOne("PharmacyPOS.Models.Account", "Account")
                        .WithMany("WishlistItems")
                        .HasForeignKey("AccountId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("PharmacyPOS.Models.PharmacyOrder", b =>
                {
                    b.HasOne("PharmacyPOS.Models.Account", "Account")
                        .WithMany("Orders")
                        .HasForeignKey("AccountId");
                });

            modelBuilder.Entity("PharmacyPOS.Models.PharmacyOrderItem", b =>
                {
                    b.HasOne("PharmacyPOS.Models.PharmacyOrder", "PharmacyOrder")
                        .WithMany("Items")
                        .HasForeignKey("PharmacyOrderId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("PharmacyPOS.Models.PaymentRecord", b =>
                {
                    b.HasOne("PharmacyPOS.Models.PharmacyOrder", "PharmacyOrder")
                        .WithOne("Payment")
                        .HasForeignKey("PharmacyPOS.Models.PaymentRecord", "PharmacyOrderId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });
#pragma warning restore 612, 618
        }
    }
}
