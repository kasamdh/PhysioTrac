using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhysioTrac.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ClinicalNoteAmendments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AmendmentReason",
                table: "ClinicalNotes",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AmendsNoteId",
                table: "ClinicalNotes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalNotes_AmendsNoteId",
                table: "ClinicalNotes",
                column: "AmendsNoteId");

            migrationBuilder.AddForeignKey(
                name: "FK_ClinicalNotes_ClinicalNotes_AmendsNoteId",
                table: "ClinicalNotes",
                column: "AmendsNoteId",
                principalTable: "ClinicalNotes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ClinicalNotes_ClinicalNotes_AmendsNoteId",
                table: "ClinicalNotes");

            migrationBuilder.DropIndex(
                name: "IX_ClinicalNotes_AmendsNoteId",
                table: "ClinicalNotes");

            migrationBuilder.DropColumn(
                name: "AmendmentReason",
                table: "ClinicalNotes");

            migrationBuilder.DropColumn(
                name: "AmendsNoteId",
                table: "ClinicalNotes");
        }
    }
}
