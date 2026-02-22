using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToolRental.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialSqlServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Zipcode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    City = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Address = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IdNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeviceTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TypeName = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Financials",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketNr = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EntryType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceId = table.Column<int>(type: "int", nullable: true),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Financials", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Services",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketNr = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ServiceType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Technician = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ServiceDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CostAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Services", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Settings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyLogo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EmailSmtp = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SmtpPort = table.Column<int>(type: "int", nullable: false),
                    EmailPassword = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SenderName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SenderEmail = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CcAddress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EmailSubject = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContractEmailTemplate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReviewEmailSubject = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReviewEmailTemplate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TemplateContract = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AszfFile = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GoogleReview = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InvoiceXml = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DefaultRentalDays = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Rentals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketNr = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    RentStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RentalDays = table.Column<int>(type: "int", nullable: false),
                    PaymentMode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Contract = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Invoice = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReviewEmailSent = table.Column<bool>(type: "bit", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ContractEmailSent = table.Column<bool>(type: "bit", nullable: false),
                    InvoiceEmailSent = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rentals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Rentals_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Devices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeviceName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DeviceType = table.Column<int>(type: "int", nullable: false),
                    Serial = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RentPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Available = table.Column<bool>(type: "bit", nullable: false),
                    Picture = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RentCount = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Devices_DeviceTypes_DeviceType",
                        column: x => x.DeviceType,
                        principalTable: "DeviceTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FinancialDevices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FinancialId = table.Column<int>(type: "int", nullable: false),
                    DeviceId = table.Column<int>(type: "int", nullable: false),
                    DeviceId1 = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinancialDevices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinancialDevices_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FinancialDevices_Devices_DeviceId1",
                        column: x => x.DeviceId1,
                        principalTable: "Devices",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FinancialDevices_Financials_FinancialId",
                        column: x => x.FinancialId,
                        principalTable: "Financials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RentalDevices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RentalId = table.Column<int>(type: "int", nullable: false),
                    DeviceId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RentalDevices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RentalDevices_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RentalDevices_Rentals_RentalId",
                        column: x => x.RentalId,
                        principalTable: "Rentals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ServiceDevices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ServiceId = table.Column<int>(type: "int", nullable: false),
                    DeviceId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceDevices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceDevices_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ServiceDevices_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Devices_DeviceType",
                table: "Devices",
                column: "DeviceType");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialDevices_DeviceId",
                table: "FinancialDevices",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialDevices_DeviceId1",
                table: "FinancialDevices",
                column: "DeviceId1");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialDevices_FinancialId",
                table: "FinancialDevices",
                column: "FinancialId");

            migrationBuilder.CreateIndex(
                name: "IX_RentalDevices_DeviceId",
                table: "RentalDevices",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_RentalDevices_RentalId",
                table: "RentalDevices",
                column: "RentalId");

            migrationBuilder.CreateIndex(
                name: "IX_Rentals_CustomerId",
                table: "Rentals",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceDevices_DeviceId",
                table: "ServiceDevices",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceDevices_ServiceId",
                table: "ServiceDevices",
                column: "ServiceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FinancialDevices");

            migrationBuilder.DropTable(
                name: "RentalDevices");

            migrationBuilder.DropTable(
                name: "ServiceDevices");

            migrationBuilder.DropTable(
                name: "Settings");

            migrationBuilder.DropTable(
                name: "Financials");

            migrationBuilder.DropTable(
                name: "Rentals");

            migrationBuilder.DropTable(
                name: "Devices");

            migrationBuilder.DropTable(
                name: "Services");

            migrationBuilder.DropTable(
                name: "Customers");

            migrationBuilder.DropTable(
                name: "DeviceTypes");
        }
    }
}
