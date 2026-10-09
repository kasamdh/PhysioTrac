using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhysioTrac.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NotePlanOfCareIndexNotUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ClinicalNotes_PlanOfCareId",
                table: "ClinicalNotes");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalNotes_PlanOfCareId",
                table: "ClinicalNotes",
                column: "PlanOfCareId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ClinicalNotes_PlanOfCareId",
                table: "ClinicalNotes");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalNotes_PlanOfCareId",
                table: "ClinicalNotes",
                column: "PlanOfCareId",
                unique: true,
                filter: "[PlanOfCareId] IS NOT NULL");
        }
    }
}
