using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhysioTrac.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NotePrefillReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PrefillReviewedAt",
                table: "ClinicalNotes",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PrefillReviewedById",
                table: "ClinicalNotes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PrefilledAt",
                table: "ClinicalNotes",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PrefilledById",
                table: "ClinicalNotes",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PrefillReviewedAt",
                table: "ClinicalNotes");

            migrationBuilder.DropColumn(
                name: "PrefillReviewedById",
                table: "ClinicalNotes");

            migrationBuilder.DropColumn(
                name: "PrefilledAt",
                table: "ClinicalNotes");

            migrationBuilder.DropColumn(
                name: "PrefilledById",
                table: "ClinicalNotes");
        }
    }
}
