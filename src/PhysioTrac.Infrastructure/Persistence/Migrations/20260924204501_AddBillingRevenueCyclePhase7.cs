using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhysioTrac.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBillingRevenueCyclePhase7 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ServicePrices_OrganizationId_CptCode",
                table: "ServicePrices");

            migrationBuilder.AddColumn<Guid>(
                name: "LocationId",
                table: "ServicePrices",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Method",
                table: "PaymentRecords",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EightMinuteRuleVariant",
                table: "Organizations",
                type: "nvarchar(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "AppointmentId",
                table: "Charges",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultCptCode",
                table: "AppointmentTypes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CptCodeMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InterventionCategory = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CptCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CptCodeMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CptCodeMappings_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CptCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsTimeBased = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CptCodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PayerFeeScheduleItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CptCode = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    AllowedAmount = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayerFeeScheduleItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayerFeeScheduleItems_Payers_PayerId",
                        column: x => x.PayerId,
                        principalTable: "Payers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServicePrices_LocationId",
                table: "ServicePrices",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_ServicePrices_OrganizationId_CptCode",
                table: "ServicePrices",
                columns: new[] { "OrganizationId", "CptCode" },
                unique: true,
                filter: "[LocationId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ServicePrices_OrganizationId_LocationId_CptCode",
                table: "ServicePrices",
                columns: new[] { "OrganizationId", "LocationId", "CptCode" },
                unique: true,
                filter: "[LocationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Charges_AppointmentId",
                table: "Charges",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_CptCodeMappings_OrganizationId_InterventionCategory_IsActive",
                table: "CptCodeMappings",
                columns: new[] { "OrganizationId", "InterventionCategory", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_CptCodes_Code",
                table: "CptCodes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayerFeeScheduleItems_PayerId_CptCode",
                table: "PayerFeeScheduleItems",
                columns: new[] { "PayerId", "CptCode" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Charges_Appointments_AppointmentId",
                table: "Charges",
                column: "AppointmentId",
                principalTable: "Appointments",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ServicePrices_Locations_LocationId",
                table: "ServicePrices",
                column: "LocationId",
                principalTable: "Locations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Charges_Appointments_AppointmentId",
                table: "Charges");

            migrationBuilder.DropForeignKey(
                name: "FK_ServicePrices_Locations_LocationId",
                table: "ServicePrices");

            migrationBuilder.DropTable(
                name: "CptCodeMappings");

            migrationBuilder.DropTable(
                name: "CptCodes");

            migrationBuilder.DropTable(
                name: "PayerFeeScheduleItems");

            migrationBuilder.DropIndex(
                name: "IX_ServicePrices_LocationId",
                table: "ServicePrices");

            migrationBuilder.DropIndex(
                name: "IX_ServicePrices_OrganizationId_CptCode",
                table: "ServicePrices");

            migrationBuilder.DropIndex(
                name: "IX_ServicePrices_OrganizationId_LocationId_CptCode",
                table: "ServicePrices");

            migrationBuilder.DropIndex(
                name: "IX_Charges_AppointmentId",
                table: "Charges");

            migrationBuilder.DropColumn(
                name: "LocationId",
                table: "ServicePrices");

            migrationBuilder.DropColumn(
                name: "Method",
                table: "PaymentRecords");

            migrationBuilder.DropColumn(
                name: "EightMinuteRuleVariant",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "AppointmentId",
                table: "Charges");

            migrationBuilder.DropColumn(
                name: "DefaultCptCode",
                table: "AppointmentTypes");

            migrationBuilder.CreateIndex(
                name: "IX_ServicePrices_OrganizationId_CptCode",
                table: "ServicePrices",
                columns: new[] { "OrganizationId", "CptCode" },
                unique: true);
        }
    }
}
