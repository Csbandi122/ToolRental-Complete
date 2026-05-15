using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToolRental.Data.Migrations
{
    /// <inheritdoc />
    public partial class ServicePartsAndWorkTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Technician",
                table: "Services");

            migrationBuilder.AddColumn<int>(
                name: "WorkHours",
                table: "Services",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WorkMinutes",
                table: "Services",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Parts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ServiceParts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ServiceId = table.Column<int>(type: "int", nullable: false),
                    PartId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceParts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceParts_Parts_PartId",
                        column: x => x.PartId,
                        principalTable: "Parts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ServiceParts_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceParts_PartId",
                table: "ServiceParts",
                column: "PartId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceParts_ServiceId",
                table: "ServiceParts",
                column: "ServiceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ServiceParts");

            migrationBuilder.DropTable(
                name: "Parts");

            migrationBuilder.DropColumn(
                name: "WorkHours",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "WorkMinutes",
                table: "Services");

            migrationBuilder.AddColumn<string>(
                name: "Technician",
                table: "Services",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");
        }
    }
}
