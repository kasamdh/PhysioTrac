using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhysioTrac.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDashboardsSaasAdminPhase8 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "Organizations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CancelledAt",
                table: "Organizations",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CancelledById",
                table: "Organizations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripeCustomerId",
                table: "Organizations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripeSubscriptionId",
                table: "Organizations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "TrialEndDate",
                table: "Organizations",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "CancelledById",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "StripeCustomerId",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "StripeSubscriptionId",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "TrialEndDate",
                table: "Organizations");
        }
    }
}
