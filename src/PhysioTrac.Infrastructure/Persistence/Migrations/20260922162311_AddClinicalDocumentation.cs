using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhysioTrac.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClinicalDocumentation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClinicalNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TherapistId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppointmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NoteType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ServiceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DiagnosisSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PrecautionsSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Subjective = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Objective = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Interventions = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Assessment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Plan = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PlanOfCareStart = table.Column<DateOnly>(type: "date", nullable: true),
                    PlanOfCareEnd = table.Column<DateOnly>(type: "date", nullable: true),
                    FrequencyPerWeek = table.Column<int>(type: "int", nullable: true),
                    DurationWeeks = table.Column<int>(type: "int", nullable: true),
                    ReassessmentDue = table.Column<DateOnly>(type: "date", nullable: true),
                    SignatureName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SignedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FinalizationAttestation = table.Column<bool>(type: "bit", nullable: false),
                    SubjectiveDetailsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ObjectiveMeasurementsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DischargeDetailsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HomeVisitDetailsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CosignRequired = table.Column<bool>(type: "bit", nullable: false),
                    CosignedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CosignedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicalNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClinicalNotes_Appointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "Appointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ClinicalNotes_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FunctionalGoals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FunctionalLimitation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FunctionalTask = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BaselineValue = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    TargetValue = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    CurrentValue = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: true),
                    Unit = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MeasurementMethod = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TargetDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SuggestedWording = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FunctionalGoals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FunctionalGoals_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NoteAddenda",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NoteAddenda", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NoteAddenda_ClinicalNotes_NoteId",
                        column: x => x.NoteId,
                        principalTable: "ClinicalNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NoteInterventions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BodyRegion = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Category = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Minutes = table.Column<int>(type: "int", nullable: false),
                    Units = table.Column<int>(type: "int", nullable: true),
                    IsTimed = table.Column<bool>(type: "bit", nullable: false),
                    PatientResponse = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Order = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NoteInterventions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NoteInterventions_ClinicalNotes_NoteId",
                        column: x => x.NoteId,
                        principalTable: "ClinicalNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OutcomeScores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RecordedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Measure = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MeasuredOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Score = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    MaximumScore = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: true),
                    ItemResponsesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutcomeScores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OutcomeScores_ClinicalNotes_NoteId",
                        column: x => x.NoteId,
                        principalTable: "ClinicalNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_OutcomeScores_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalNotes_AppointmentId",
                table: "ClinicalNotes",
                column: "AppointmentId",
                unique: true,
                filter: "[AppointmentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalNotes_PatientId_ServiceDate",
                table: "ClinicalNotes",
                columns: new[] { "PatientId", "ServiceDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalNotes_Status_ReassessmentDue",
                table: "ClinicalNotes",
                columns: new[] { "Status", "ReassessmentDue" });

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalNotes_TherapistId_ServiceDate",
                table: "ClinicalNotes",
                columns: new[] { "TherapistId", "ServiceDate" });

            migrationBuilder.CreateIndex(
                name: "IX_FunctionalGoals_PatientId_Status_TargetDate",
                table: "FunctionalGoals",
                columns: new[] { "PatientId", "Status", "TargetDate" });

            migrationBuilder.CreateIndex(
                name: "IX_NoteAddenda_NoteId",
                table: "NoteAddenda",
                column: "NoteId");

            migrationBuilder.CreateIndex(
                name: "IX_NoteInterventions_NoteId",
                table: "NoteInterventions",
                column: "NoteId");

            migrationBuilder.CreateIndex(
                name: "IX_OutcomeScores_NoteId",
                table: "OutcomeScores",
                column: "NoteId");

            migrationBuilder.CreateIndex(
                name: "IX_OutcomeScores_PatientId_Measure_MeasuredOn",
                table: "OutcomeScores",
                columns: new[] { "PatientId", "Measure", "MeasuredOn" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FunctionalGoals");

            migrationBuilder.DropTable(
                name: "NoteAddenda");

            migrationBuilder.DropTable(
                name: "NoteInterventions");

            migrationBuilder.DropTable(
                name: "OutcomeScores");

            migrationBuilder.DropTable(
                name: "ClinicalNotes");
        }
    }
}
