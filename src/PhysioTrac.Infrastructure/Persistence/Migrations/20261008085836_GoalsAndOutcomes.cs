using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhysioTrac.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GoalsAndOutcomes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Interpretation",
                table: "OutcomeScores",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Comments",
                table: "FunctionalGoals",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "FunctionalGoals",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "FunctionalGoalHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GoalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    GoalVersion = table.Column<int>(type: "int", nullable: false),
                    NoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RecordedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CurrentValue = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: true),
                    ProgressPercent = table.Column<int>(type: "int", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FunctionalGoalHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FunctionalGoalHistory_FunctionalGoals_GoalId",
                        column: x => x.GoalId,
                        principalTable: "FunctionalGoals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NoteGoalProgress",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GoalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    GoalVersion = table.Column<int>(type: "int", nullable: false),
                    Term = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    FunctionalTask = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FunctionalLimitation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BaselineValue = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    TargetValue = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MeasurementMethod = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TargetDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PreviousValue = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: true),
                    CurrentValue = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IncludeInNarrative = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NoteGoalProgress", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NoteGoalProgress_ClinicalNotes_NoteId",
                        column: x => x.NoteId,
                        principalTable: "ClinicalNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NoteGoalProgress_FunctionalGoals_GoalId",
                        column: x => x.GoalId,
                        principalTable: "FunctionalGoals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FunctionalGoalHistory_GoalId_CreatedAt",
                table: "FunctionalGoalHistory",
                columns: new[] { "GoalId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FunctionalGoalHistory_NoteId",
                table: "FunctionalGoalHistory",
                column: "NoteId");

            migrationBuilder.CreateIndex(
                name: "IX_NoteGoalProgress_GoalId",
                table: "NoteGoalProgress",
                column: "GoalId");

            migrationBuilder.CreateIndex(
                name: "IX_NoteGoalProgress_NoteId_GoalId",
                table: "NoteGoalProgress",
                columns: new[] { "NoteId", "GoalId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FunctionalGoalHistory");

            migrationBuilder.DropTable(
                name: "NoteGoalProgress");

            migrationBuilder.DropColumn(
                name: "Interpretation",
                table: "OutcomeScores");

            migrationBuilder.DropColumn(
                name: "Comments",
                table: "FunctionalGoals");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "FunctionalGoals");
        }
    }
}
