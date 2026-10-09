using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhysioTrac.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExerciseLibrary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Exercises",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    PatientDescription = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ClinicalPurpose = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    BodyRegion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TargetMuscles = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    StartingPosition = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Instructions = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    EndingPosition = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Equipment = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Difficulty = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Position = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Laterality = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BreathingInstructions = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CommonMistakes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SafetyPrecautions = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Contraindications = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Progressions = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Regressions = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    VideoUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    NeedsClinicalReview = table.Column<bool>(type: "bit", nullable: false),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReviewedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Exercises", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Exercises_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExerciseMedia",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExerciseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StorageKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ThumbnailStorageKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Width = table.Column<int>(type: "int", nullable: false),
                    Height = table.Column<int>(type: "int", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    AltText = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Caption = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    SourceAttribution = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    OriginalFileName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RetiredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReplacedByMediaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExerciseMedia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExerciseMedia_Exercises_ExerciseId",
                        column: x => x.ExerciseId,
                        principalTable: "Exercises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExerciseMedia_ExerciseId_RetiredAt_Sequence",
                table: "ExerciseMedia",
                columns: new[] { "ExerciseId", "RetiredAt", "Sequence" });

            migrationBuilder.CreateIndex(
                name: "IX_Exercises_Code",
                table: "Exercises",
                column: "Code",
                unique: true,
                filter: "[OrganizationId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Exercises_OrganizationId_Code",
                table: "Exercises",
                columns: new[] { "OrganizationId", "Code" },
                unique: true,
                filter: "[OrganizationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Exercises_OrganizationId_IsActive_BodyRegion",
                table: "Exercises",
                columns: new[] { "OrganizationId", "IsActive", "BodyRegion" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExerciseMedia");

            migrationBuilder.DropTable(
                name: "Exercises");
        }
    }
}
