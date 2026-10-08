using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhysioTrac.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DocumentationFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PlanOfCareId",
                table: "FunctionalGoals",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "ClinicalNoteTemplates",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSystem",
                table: "ClinicalNoteTemplates",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Specialty",
                table: "ClinicalNoteTemplates",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TemplateKey",
                table: "ClinicalNoteTemplates",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedById",
                table: "ClinicalNoteTemplates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "ClinicalNotes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PeriodEnd",
                table: "ClinicalNotes",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PeriodStart",
                table: "ClinicalNotes",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PlanOfCareId",
                table: "ClinicalNotes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReturnReason",
                table: "ClinicalNotes",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SubmittedAt",
                table: "ClinicalNotes",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SupervisingProviderId",
                table: "ClinicalNotes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TemplateVersionId",
                table: "ClinicalNotes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TreatingProviderId",
                table: "ClinicalNotes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedById",
                table: "ClinicalNotes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VoidReason",
                table: "ClinicalNotes",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "VoidedAt",
                table: "ClinicalNotes",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VoidedById",
                table: "ClinicalNotes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DefaultNoteType",
                table: "AppointmentTypes",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ClinicalNoteStatusChanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ToStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ChangedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicalNoteStatusChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClinicalNoteStatusChanges_ClinicalNotes_NoteId",
                        column: x => x.NoteId,
                        principalTable: "ClinicalNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClinicalNoteTemplateAppointmentTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppointmentTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicalNoteTemplateAppointmentTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClinicalNoteTemplateAppointmentTypes_AppointmentTypes_AppointmentTypeId",
                        column: x => x.AppointmentTypeId,
                        principalTable: "AppointmentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClinicalNoteTemplateAppointmentTypes_ClinicalNoteTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "ClinicalNoteTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClinicalNoteTemplateVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    ChangeSummary = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicalNoteTemplateVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClinicalNoteTemplateVersions_ClinicalNoteTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "ClinicalNoteTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ElectronicSignatures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SignerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SignerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Credentials = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Role = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Meaning = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SignedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DisplayTimeZone = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    NoteVersionNumber = table.Column<int>(type: "int", nullable: false),
                    ContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Method = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ElectronicSignatures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ElectronicSignatures_ClinicalNotes_NoteId",
                        column: x => x.NoteId,
                        principalTable: "ClinicalNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NoteAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Caption = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NoteAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NoteAttachments_ClinicalNotes_NoteId",
                        column: x => x.NoteId,
                        principalTable: "ClinicalNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NoteAttachments_PatientDocuments_PatientDocumentId",
                        column: x => x.PatientDocumentId,
                        principalTable: "PatientDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NoteCosignRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupervisorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ResolvedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResolutionComment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NoteCosignRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NoteCosignRequests_ClinicalNotes_NoteId",
                        column: x => x.NoteId,
                        principalTable: "ClinicalNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlansOfCare",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceNoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreviousPlanOfCareId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    FrequencyPerWeek = table.Column<int>(type: "int", nullable: true),
                    DurationWeeks = table.Column<int>(type: "int", nullable: true),
                    TreatmentDiagnosis = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Prognosis = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RehabPotential = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PlannedInterventions = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    HomeProgram = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    PatientEducation = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Referrals = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CertifiedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CertifyingProviderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DischargeReason = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    DischargeNoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlansOfCare", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlansOfCare_ClinicalNotes_SourceNoteId",
                        column: x => x.SourceNoteId,
                        principalTable: "ClinicalNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlansOfCare_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlansOfCare_PlansOfCare_PreviousPlanOfCareId",
                        column: x => x.PreviousPlanOfCareId,
                        principalTable: "PlansOfCare",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlansOfCare_ReferringProviders_CertifyingProviderId",
                        column: x => x.CertifyingProviderId,
                        principalTable: "ReferringProviders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProviderFavorites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderFavorites", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClinicalNoteTemplateSections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    HelpText = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    Component = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicalNoteTemplateSections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClinicalNoteTemplateSections_ClinicalNoteTemplateVersions_VersionId",
                        column: x => x.VersionId,
                        principalTable: "ClinicalNoteTemplateVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClinicalNoteTemplateFields",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FieldType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    HelpText = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Placeholder = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Unit = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    NoteColumn = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ConfigJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ValidationJson = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ConditionJson = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicalNoteTemplateFields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClinicalNoteTemplateFields_ClinicalNoteTemplateSections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "ClinicalNoteTemplateSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClinicalNoteTemplateFields_ClinicalNoteTemplateVersions_VersionId",
                        column: x => x.VersionId,
                        principalTable: "ClinicalNoteTemplateVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClinicalNoteFieldValues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FieldId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FieldKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ValueText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ValueNumber = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    ValueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ValueTime = table.Column<TimeOnly>(type: "time", nullable: true),
                    ValueBool = table.Column<bool>(type: "bit", nullable: true),
                    ValueJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicalNoteFieldValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClinicalNoteFieldValues_ClinicalNoteTemplateFields_FieldId",
                        column: x => x.FieldId,
                        principalTable: "ClinicalNoteTemplateFields",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClinicalNoteFieldValues_ClinicalNotes_NoteId",
                        column: x => x.NoteId,
                        principalTable: "ClinicalNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FunctionalGoals_PlanOfCareId",
                table: "FunctionalGoals",
                column: "PlanOfCareId");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalNoteTemplates_TemplateKey",
                table: "ClinicalNoteTemplates",
                column: "TemplateKey");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalNotes_PlanOfCareId",
                table: "ClinicalNotes",
                column: "PlanOfCareId",
                unique: true,
                filter: "[PlanOfCareId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalNotes_Status_ServiceDate",
                table: "ClinicalNotes",
                columns: new[] { "Status", "ServiceDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalNotes_SupervisingProviderId",
                table: "ClinicalNotes",
                column: "SupervisingProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalNotes_TemplateVersionId",
                table: "ClinicalNotes",
                column: "TemplateVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalNotes_TreatingProviderId_ServiceDate",
                table: "ClinicalNotes",
                columns: new[] { "TreatingProviderId", "ServiceDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalNoteFieldValues_FieldId",
                table: "ClinicalNoteFieldValues",
                column: "FieldId");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalNoteFieldValues_NoteId_FieldKey",
                table: "ClinicalNoteFieldValues",
                columns: new[] { "NoteId", "FieldKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalNoteStatusChanges_NoteId_CreatedAt",
                table: "ClinicalNoteStatusChanges",
                columns: new[] { "NoteId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalNoteTemplateAppointmentTypes_AppointmentTypeId",
                table: "ClinicalNoteTemplateAppointmentTypes",
                column: "AppointmentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalNoteTemplateAppointmentTypes_TemplateId_AppointmentTypeId",
                table: "ClinicalNoteTemplateAppointmentTypes",
                columns: new[] { "TemplateId", "AppointmentTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalNoteTemplateFields_SectionId_DisplayOrder",
                table: "ClinicalNoteTemplateFields",
                columns: new[] { "SectionId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalNoteTemplateFields_VersionId_Key",
                table: "ClinicalNoteTemplateFields",
                columns: new[] { "VersionId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalNoteTemplateSections_VersionId_DisplayOrder",
                table: "ClinicalNoteTemplateSections",
                columns: new[] { "VersionId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalNoteTemplateSections_VersionId_Key",
                table: "ClinicalNoteTemplateSections",
                columns: new[] { "VersionId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalNoteTemplateVersions_TemplateId_VersionNumber",
                table: "ClinicalNoteTemplateVersions",
                columns: new[] { "TemplateId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ElectronicSignatures_NoteId_SignedAt",
                table: "ElectronicSignatures",
                columns: new[] { "NoteId", "SignedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ElectronicSignatures_SignerUserId",
                table: "ElectronicSignatures",
                column: "SignerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_NoteAttachments_NoteId_PatientDocumentId",
                table: "NoteAttachments",
                columns: new[] { "NoteId", "PatientDocumentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NoteAttachments_PatientDocumentId",
                table: "NoteAttachments",
                column: "PatientDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_NoteCosignRequests_NoteId",
                table: "NoteCosignRequests",
                column: "NoteId");

            migrationBuilder.CreateIndex(
                name: "IX_NoteCosignRequests_Status_SupervisorUserId",
                table: "NoteCosignRequests",
                columns: new[] { "Status", "SupervisorUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlansOfCare_CertifyingProviderId",
                table: "PlansOfCare",
                column: "CertifyingProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_PlansOfCare_PatientId_Status",
                table: "PlansOfCare",
                columns: new[] { "PatientId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PlansOfCare_PreviousPlanOfCareId",
                table: "PlansOfCare",
                column: "PreviousPlanOfCareId");

            migrationBuilder.CreateIndex(
                name: "IX_PlansOfCare_SourceNoteId",
                table: "PlansOfCare",
                column: "SourceNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_PlansOfCare_Status_EndDate",
                table: "PlansOfCare",
                columns: new[] { "Status", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ProviderFavorites_UserId_ItemType_ItemId",
                table: "ProviderFavorites",
                columns: new[] { "UserId", "ItemType", "ItemId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ClinicalNotes_ClinicalNoteTemplateVersions_TemplateVersionId",
                table: "ClinicalNotes",
                column: "TemplateVersionId",
                principalTable: "ClinicalNoteTemplateVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ClinicalNotes_PlansOfCare_PlanOfCareId",
                table: "ClinicalNotes",
                column: "PlanOfCareId",
                principalTable: "PlansOfCare",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ClinicalNotes_Providers_SupervisingProviderId",
                table: "ClinicalNotes",
                column: "SupervisingProviderId",
                principalTable: "Providers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ClinicalNotes_Providers_TreatingProviderId",
                table: "ClinicalNotes",
                column: "TreatingProviderId",
                principalTable: "Providers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FunctionalGoals_PlansOfCare_PlanOfCareId",
                table: "FunctionalGoals",
                column: "PlanOfCareId",
                principalTable: "PlansOfCare",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ClinicalNotes_ClinicalNoteTemplateVersions_TemplateVersionId",
                table: "ClinicalNotes");

            migrationBuilder.DropForeignKey(
                name: "FK_ClinicalNotes_PlansOfCare_PlanOfCareId",
                table: "ClinicalNotes");

            migrationBuilder.DropForeignKey(
                name: "FK_ClinicalNotes_Providers_SupervisingProviderId",
                table: "ClinicalNotes");

            migrationBuilder.DropForeignKey(
                name: "FK_ClinicalNotes_Providers_TreatingProviderId",
                table: "ClinicalNotes");

            migrationBuilder.DropForeignKey(
                name: "FK_FunctionalGoals_PlansOfCare_PlanOfCareId",
                table: "FunctionalGoals");

            migrationBuilder.DropTable(
                name: "ClinicalNoteFieldValues");

            migrationBuilder.DropTable(
                name: "ClinicalNoteStatusChanges");

            migrationBuilder.DropTable(
                name: "ClinicalNoteTemplateAppointmentTypes");

            migrationBuilder.DropTable(
                name: "ElectronicSignatures");

            migrationBuilder.DropTable(
                name: "NoteAttachments");

            migrationBuilder.DropTable(
                name: "NoteCosignRequests");

            migrationBuilder.DropTable(
                name: "PlansOfCare");

            migrationBuilder.DropTable(
                name: "ProviderFavorites");

            migrationBuilder.DropTable(
                name: "ClinicalNoteTemplateFields");

            migrationBuilder.DropTable(
                name: "ClinicalNoteTemplateSections");

            migrationBuilder.DropTable(
                name: "ClinicalNoteTemplateVersions");

            migrationBuilder.DropIndex(
                name: "IX_FunctionalGoals_PlanOfCareId",
                table: "FunctionalGoals");

            migrationBuilder.DropIndex(
                name: "IX_ClinicalNoteTemplates_TemplateKey",
                table: "ClinicalNoteTemplates");

            migrationBuilder.DropIndex(
                name: "IX_ClinicalNotes_PlanOfCareId",
                table: "ClinicalNotes");

            migrationBuilder.DropIndex(
                name: "IX_ClinicalNotes_Status_ServiceDate",
                table: "ClinicalNotes");

            migrationBuilder.DropIndex(
                name: "IX_ClinicalNotes_SupervisingProviderId",
                table: "ClinicalNotes");

            migrationBuilder.DropIndex(
                name: "IX_ClinicalNotes_TemplateVersionId",
                table: "ClinicalNotes");

            migrationBuilder.DropIndex(
                name: "IX_ClinicalNotes_TreatingProviderId_ServiceDate",
                table: "ClinicalNotes");

            migrationBuilder.DropColumn(
                name: "PlanOfCareId",
                table: "FunctionalGoals");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "ClinicalNoteTemplates");

            migrationBuilder.DropColumn(
                name: "IsSystem",
                table: "ClinicalNoteTemplates");

            migrationBuilder.DropColumn(
                name: "Specialty",
                table: "ClinicalNoteTemplates");

            migrationBuilder.DropColumn(
                name: "TemplateKey",
                table: "ClinicalNoteTemplates");

            migrationBuilder.DropColumn(
                name: "UpdatedById",
                table: "ClinicalNoteTemplates");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "ClinicalNotes");

            migrationBuilder.DropColumn(
                name: "PeriodEnd",
                table: "ClinicalNotes");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                table: "ClinicalNotes");

            migrationBuilder.DropColumn(
                name: "PlanOfCareId",
                table: "ClinicalNotes");

            migrationBuilder.DropColumn(
                name: "ReturnReason",
                table: "ClinicalNotes");

            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                table: "ClinicalNotes");

            migrationBuilder.DropColumn(
                name: "SupervisingProviderId",
                table: "ClinicalNotes");

            migrationBuilder.DropColumn(
                name: "TemplateVersionId",
                table: "ClinicalNotes");

            migrationBuilder.DropColumn(
                name: "TreatingProviderId",
                table: "ClinicalNotes");

            migrationBuilder.DropColumn(
                name: "UpdatedById",
                table: "ClinicalNotes");

            migrationBuilder.DropColumn(
                name: "VoidReason",
                table: "ClinicalNotes");

            migrationBuilder.DropColumn(
                name: "VoidedAt",
                table: "ClinicalNotes");

            migrationBuilder.DropColumn(
                name: "VoidedById",
                table: "ClinicalNotes");

            migrationBuilder.DropColumn(
                name: "DefaultNoteType",
                table: "AppointmentTypes");
        }
    }
}
