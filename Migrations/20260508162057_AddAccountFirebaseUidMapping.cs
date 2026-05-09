using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmacyPOS.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountFirebaseUidMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FirebaseUid",
                table: "Accounts",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FirebaseUid",
                table: "Accounts");
        }
    }
}
