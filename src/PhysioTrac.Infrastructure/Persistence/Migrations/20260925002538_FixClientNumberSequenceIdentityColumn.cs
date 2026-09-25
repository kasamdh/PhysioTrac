using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhysioTrac.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixClientNumberSequenceIdentityColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SQL Server has no in-place ALTER COLUMN to drop IDENTITY --
            // EF Core's own migration generator refuses to even try (throws
            // "the column needs to be dropped and recreated" at apply time,
            // caught live rather than by the auto-generated migration).
            // ClientNumberSequences is always empty or a single Id=1 row
            // (see the entity's own doc comment), so a drop-and-recreate is
            // safe -- there is no data to preserve across it.
            migrationBuilder.DropTable(name: "ClientNumberSequences");
            migrationBuilder.CreateTable(
                name: "ClientNumberSequences",
                columns: table => new
                {
                    Id = table.Column<short>(type: "smallint", nullable: false),
                    NextNumber = table.Column<long>(type: "bigint", nullable: false),
                },
                constraints: table => table.PrimaryKey("PK_ClientNumberSequences", x => x.Id));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ClientNumberSequences");
            migrationBuilder.CreateTable(
                name: "ClientNumberSequences",
                columns: table => new
                {
                    Id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NextNumber = table.Column<long>(type: "bigint", nullable: false),
                },
                constraints: table => table.PrimaryKey("PK_ClientNumberSequences", x => x.Id));
        }
    }
}
