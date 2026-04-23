using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToolRental.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceReservedUntil : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ReservedUntil",
                table: "Devices",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReservedUntil",
                table: "Devices");
        }
    }
}
