using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmacyPOS.Migrations
{
    /// <inheritdoc />
    public partial class AddFirebaseOrderSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomerUid",
                table: "Orders",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomerUid",
                table: "Orders");
        }
    }
}
