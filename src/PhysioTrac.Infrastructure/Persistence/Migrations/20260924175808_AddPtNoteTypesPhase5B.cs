using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhysioTrac.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPtNoteTypesPhase5B : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProgressNoteDueDays",
                table: "Organizations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProgressNoteDueVisitCount",
                table: "Organizations",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "NoteType",
                table: "ClinicalNoteTemplates",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "NoteType",
                table: "ClinicalNotes",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PlanOfCareCertifiedDate",
                table: "ClinicalNotes",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PlanOfCareCertifyingProviderId",
                table: "ClinicalNotes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalNotes_PlanOfCareCertifyingProviderId",
                table: "ClinicalNotes",
                column: "PlanOfCareCertifyingProviderId");

            migrationBuilder.AddForeignKey(
                name: "FK_ClinicalNotes_ReferringProviders_PlanOfCareCertifyingProviderId",
                table: "ClinicalNotes",
                column: "PlanOfCareCertifyingProviderId",
                principalTable: "ReferringProviders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ClinicalNotes_ReferringProviders_PlanOfCareCertifyingProviderId",
                table: "ClinicalNotes");

            migrationBuilder.DropIndex(
                name: "IX_ClinicalNotes_PlanOfCareCertifyingProviderId",
                table: "ClinicalNotes");

            migrationBuilder.DropColumn(
                name: "ProgressNoteDueDays",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "ProgressNoteDueVisitCount",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "PlanOfCareCertifiedDate",
                table: "ClinicalNotes");

            migrationBuilder.DropColumn(
                name: "PlanOfCareCertifyingProviderId",
                table: "ClinicalNotes");

            migrationBuilder.AlterColumn<string>(
                name: "NoteType",
                table: "ClinicalNoteTemplates",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<string>(
                name: "NoteType",
                table: "ClinicalNotes",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30);
        }
    }
}
