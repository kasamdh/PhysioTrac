using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhysioTrac.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SchedulingPhase1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Discipline",
                table: "Providers",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: "Other");

            migrationBuilder.AddColumn<bool>(
                name: "AllowDoubleBookOverride",
                table: "BookingConfigurations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "StaffSlotMinutes",
                table: "BookingConfigurations",
                type: "int",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<bool>(
                name: "TherapistsMayReschedule",
                table: "BookingConfigurations",
                type: "bit",
                nullable: false,
                defaultValue: true);

            // Backfill discipline from the free-text credentials existing
            // providers already have ("PT, DPT", "PTA", ...). PTA first, since
            // "PTA" also contains "PT". Anything else stays "Other" until an
            // admin sets it.
            migrationBuilder.Sql(
                "UPDATE Providers SET Discipline = 'PTA' " +
                "WHERE ',' + REPLACE(REPLACE(UPPER(ISNULL(Credentials, '')), ' ', ''), '.', '') + ',' LIKE '%,PTA,%';");
            migrationBuilder.Sql(
                "UPDATE Providers SET Discipline = 'PT' " +
                "WHERE Discipline = 'Other' " +
                "AND ',' + REPLACE(REPLACE(UPPER(ISNULL(Credentials, '')), ' ', ''), '.', '') + ',' LIKE '%,PT,%';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Discipline",
                table: "Providers");

            migrationBuilder.DropColumn(
                name: "AllowDoubleBookOverride",
                table: "BookingConfigurations");

            migrationBuilder.DropColumn(
                name: "StaffSlotMinutes",
                table: "BookingConfigurations");

            migrationBuilder.DropColumn(
                name: "TherapistsMayReschedule",
                table: "BookingConfigurations");
        }
    }
}
