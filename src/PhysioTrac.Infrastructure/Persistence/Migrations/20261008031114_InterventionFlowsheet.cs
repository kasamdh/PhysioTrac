using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhysioTrac.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InterventionFlowsheet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_NoteInterventions_ClinicalNotes_NoteId",
                table: "NoteInterventions");

            migrationBuilder.AlterColumn<string>(
                name: "PatientResponse",
                table: "NoteInterventions",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "NoteInterventions",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "BodyRegion",
                table: "NoteInterventions",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssistanceLevel",
                table: "NoteInterventions",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CarriedForwardFromNoteId",
                table: "NoteInterventions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CarryForwardReviewed",
                table: "NoteInterventions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Comment",
                table: "NoteInterventions",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CptCode",
                table: "NoteInterventions",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Cueing",
                table: "NoteInterventions",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Distance",
                table: "NoteInterventions",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Duration",
                table: "NoteInterventions",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "EndTime",
                table: "NoteInterventions",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Equipment",
                table: "NoteInterventions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LibraryItemId",
                table: "NoteInterventions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Modification",
                table: "NoteInterventions",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PainAfter",
                table: "NoteInterventions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PainBefore",
                table: "NoteInterventions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Position",
                table: "NoteInterventions",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Repetitions",
                table: "NoteInterventions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Resistance",
                table: "NoteInterventions",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Sets",
                table: "NoteInterventions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "StartTime",
                table: "NoteInterventions",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "NoteInterventions",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "InterventionGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterventionGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InterventionLibraryItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CptCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    IsTimed = table.Column<bool>(type: "bit", nullable: false),
                    BodyRegion = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DefaultSets = table.Column<int>(type: "int", nullable: true),
                    DefaultRepetitions = table.Column<int>(type: "int", nullable: true),
                    DefaultResistance = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    DefaultDuration = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    DefaultEquipment = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DefaultPosition = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
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
                    table.PrimaryKey("PK_InterventionLibraryItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InterventionGroupItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LibraryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CptCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    IsTimed = table.Column<bool>(type: "bit", nullable: false),
                    Sets = table.Column<int>(type: "int", nullable: true),
                    Repetitions = table.Column<int>(type: "int", nullable: true),
                    Resistance = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Duration = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Equipment = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Position = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Order = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterventionGroupItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InterventionGroupItems_InterventionGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "InterventionGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InterventionGroupItems_InterventionLibraryItems_LibraryItemId",
                        column: x => x.LibraryItemId,
                        principalTable: "InterventionLibraryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NoteInterventions_CarriedForwardFromNoteId",
                table: "NoteInterventions",
                column: "CarriedForwardFromNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_NoteInterventions_LibraryItemId",
                table: "NoteInterventions",
                column: "LibraryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InterventionGroupItems_GroupId_Order",
                table: "InterventionGroupItems",
                columns: new[] { "GroupId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_InterventionGroupItems_LibraryItemId",
                table: "InterventionGroupItems",
                column: "LibraryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InterventionGroups_OwnerUserId_IsActive",
                table: "InterventionGroups",
                columns: new[] { "OwnerUserId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_InterventionLibraryItems_Category_IsActive",
                table: "InterventionLibraryItems",
                columns: new[] { "Category", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_InterventionLibraryItems_Code",
                table: "InterventionLibraryItems",
                column: "Code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_NoteInterventions_ClinicalNotes_NoteId",
                table: "NoteInterventions",
                column: "NoteId",
                principalTable: "ClinicalNotes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_NoteInterventions_InterventionLibraryItems_LibraryItemId",
                table: "NoteInterventions",
                column: "LibraryItemId",
                principalTable: "InterventionLibraryItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_NoteInterventions_ClinicalNotes_NoteId",
                table: "NoteInterventions");

            migrationBuilder.DropForeignKey(
                name: "FK_NoteInterventions_InterventionLibraryItems_LibraryItemId",
                table: "NoteInterventions");

            migrationBuilder.DropTable(
                name: "InterventionGroupItems");

            migrationBuilder.DropTable(
                name: "InterventionGroups");

            migrationBuilder.DropTable(
                name: "InterventionLibraryItems");

            migrationBuilder.DropIndex(
                name: "IX_NoteInterventions_CarriedForwardFromNoteId",
                table: "NoteInterventions");

            migrationBuilder.DropIndex(
                name: "IX_NoteInterventions_LibraryItemId",
                table: "NoteInterventions");

            migrationBuilder.DropColumn(
                name: "AssistanceLevel",
                table: "NoteInterventions");

            migrationBuilder.DropColumn(
                name: "CarriedForwardFromNoteId",
                table: "NoteInterventions");

            migrationBuilder.DropColumn(
                name: "CarryForwardReviewed",
                table: "NoteInterventions");

            migrationBuilder.DropColumn(
                name: "Comment",
                table: "NoteInterventions");

            migrationBuilder.DropColumn(
                name: "CptCode",
                table: "NoteInterventions");

            migrationBuilder.DropColumn(
                name: "Cueing",
                table: "NoteInterventions");

            migrationBuilder.DropColumn(
                name: "Distance",
                table: "NoteInterventions");

            migrationBuilder.DropColumn(
                name: "Duration",
                table: "NoteInterventions");

            migrationBuilder.DropColumn(
                name: "EndTime",
                table: "NoteInterventions");

            migrationBuilder.DropColumn(
                name: "Equipment",
                table: "NoteInterventions");

            migrationBuilder.DropColumn(
                name: "LibraryItemId",
                table: "NoteInterventions");

            migrationBuilder.DropColumn(
                name: "Modification",
                table: "NoteInterventions");

            migrationBuilder.DropColumn(
                name: "PainAfter",
                table: "NoteInterventions");

            migrationBuilder.DropColumn(
                name: "PainBefore",
                table: "NoteInterventions");

            migrationBuilder.DropColumn(
                name: "Position",
                table: "NoteInterventions");

            migrationBuilder.DropColumn(
                name: "Repetitions",
                table: "NoteInterventions");

            migrationBuilder.DropColumn(
                name: "Resistance",
                table: "NoteInterventions");

            migrationBuilder.DropColumn(
                name: "Sets",
                table: "NoteInterventions");

            migrationBuilder.DropColumn(
                name: "StartTime",
                table: "NoteInterventions");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "NoteInterventions");

            migrationBuilder.AlterColumn<string>(
                name: "PatientResponse",
                table: "NoteInterventions",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "NoteInterventions",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(300)",
                oldMaxLength: 300);

            migrationBuilder.AlterColumn<string>(
                name: "BodyRegion",
                table: "NoteInterventions",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(60)",
                oldMaxLength: 60,
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_NoteInterventions_ClinicalNotes_NoteId",
                table: "NoteInterventions",
                column: "NoteId",
                principalTable: "ClinicalNotes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
