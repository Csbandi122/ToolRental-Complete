using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToolRental.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSettingsFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Aszf",
                table: "Settings",
                newName: "ContractEmailTemplate");

            migrationBuilder.AddColumn<string>(
                name: "AszfFile",
                table: "Settings",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AszfFile",
                table: "Settings");

            migrationBuilder.RenameColumn(
                name: "ContractEmailTemplate",
                table: "Settings",
                newName: "Aszf");
        }
    }
}
