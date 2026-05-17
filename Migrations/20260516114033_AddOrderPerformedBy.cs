using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmacyPOS.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderPerformedBy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PerformedByName",
                table: "Orders",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PerformedByRole",
                table: "Orders",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                "UPDATE Orders SET PerformedByName = COALESCE(NULLIF(CustomerFullName, ''), 'Customer'), PerformedByRole = 'Customer' WHERE PerformedByName = '' AND PerformedByRole = ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PerformedByName",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PerformedByRole",
                table: "Orders");
        }
    }
}
