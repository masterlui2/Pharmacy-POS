using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PharmacyPOS.Data;

#nullable disable

namespace PharmacyPOS.Migrations
{
    [DbContextAttribute(typeof(PharmacyPosDbContext))]
    [Migration("20260413070000_InitialAccountStore")]
    public partial class InitialAccountStore : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Accounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomerAddresses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountId = table.Column<int>(type: "int", nullable: true),
                    FullName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    DeliveryAddress = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Landmark = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    AddressType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerAddresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerAddresses_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    AccountId = table.Column<int>(type: "int", nullable: true),
                    CustomerFullName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CustomerEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CustomerPhoneNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    DeliveryAddress = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Landmark = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    AddressType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    DeliveryOption = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    PaymentMethod = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    FulfillmentBranch = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    PrescriptionStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    OrderStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    RequiresPrescription = table.Column<bool>(type: "bit", nullable: false),
                    EstimatedDeliveryMinMinutes = table.Column<int>(type: "int", nullable: false),
                    EstimatedDeliveryMaxMinutes = table.Column<int>(type: "int", nullable: false),
                    SubtotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ShippingAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PromoCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    PrescriptionFilesJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Orders_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "OrderItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PharmacyOrderId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ProductName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BrandName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    RequiresPrescription = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderItems_Orders_PharmacyOrderId",
                        column: x => x.PharmacyOrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_Email",
                table: "Accounts",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAddresses_AccountId",
                table: "CustomerAddresses",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_PharmacyOrderId",
                table: "OrderItems",
                column: "PharmacyOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_AccountId",
                table: "Orders",
                column: "AccountId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "CustomerAddresses");
            migrationBuilder.DropTable(name: "OrderItems");
            migrationBuilder.DropTable(name: "Orders");
            migrationBuilder.DropTable(name: "Accounts");
        }

        protected override void BuildTargetModel(ModelBuilder modelBuilder)
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

            modelBuilder.Entity("PharmacyPOS.Models.CustomerAddress", b =>
                {
                    b.HasOne("PharmacyPOS.Models.Account", "Account")
                        .WithMany("CustomerAddresses")
                        .HasForeignKey("AccountId");
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
#pragma warning restore 612, 618
        }
    }
}
