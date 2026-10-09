using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhysioTrac.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MeasurementsAndSpecialTests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ObjectiveMeasurements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BodyRegion = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Item = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Movement = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Side = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Mode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    NumericValue = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    TextValue = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Unit = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    EndFeel = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Painful = table.Column<bool>(type: "bit", nullable: true),
                    Compensation = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    AssistiveDevice = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AssistanceLevel = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Surface = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Condition = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Order = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ObjectiveMeasurements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ObjectiveMeasurements_ClinicalNotes_NoteId",
                        column: x => x.NoteId,
                        principalTable: "ClinicalNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SpecialTestDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Specialty = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    BodyRegion = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ResultKind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    InterpretationGuide = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ContraindicationWarning = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpecialTestDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SpecialTestResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TestName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Specialty = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    BodyRegion = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Side = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Outcome = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NumericValue = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    Unit = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Interpretation = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Order = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpecialTestResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SpecialTestResults_ClinicalNotes_NoteId",
                        column: x => x.NoteId,
                        principalTable: "ClinicalNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SpecialTestResults_SpecialTestDefinitions_DefinitionId",
                        column: x => x.DefinitionId,
                        principalTable: "SpecialTestDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ObjectiveMeasurements_NoteId_Order",
                table: "ObjectiveMeasurements",
                columns: new[] { "NoteId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_ObjectiveMeasurements_PatientId_Category",
                table: "ObjectiveMeasurements",
                columns: new[] { "PatientId", "Category" });

            migrationBuilder.CreateIndex(
                name: "IX_SpecialTestDefinitions_Code",
                table: "SpecialTestDefinitions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SpecialTestDefinitions_Specialty_IsActive",
                table: "SpecialTestDefinitions",
                columns: new[] { "Specialty", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_SpecialTestResults_DefinitionId",
                table: "SpecialTestResults",
                column: "DefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_SpecialTestResults_NoteId_Order",
                table: "SpecialTestResults",
                columns: new[] { "NoteId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_SpecialTestResults_PatientId_TestName",
                table: "SpecialTestResults",
                columns: new[] { "PatientId", "TestName" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ObjectiveMeasurements");

            migrationBuilder.DropTable(
                name: "SpecialTestResults");

            migrationBuilder.DropTable(
                name: "SpecialTestDefinitions");
        }
    }
}
