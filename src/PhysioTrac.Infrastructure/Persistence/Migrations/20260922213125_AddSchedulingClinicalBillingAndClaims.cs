using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhysioTrac.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSchedulingClinicalBillingAndClaims : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppointmentTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DefaultDurationMinutes = table.Column<int>(type: "int", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: true),
                    Color = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    OnlineBookingEnabled = table.Column<bool>(type: "bit", nullable: false),
                    RequiresNewPatient = table.Column<bool>(type: "bit", nullable: false),
                    BufferBeforeMinutes = table.Column<int>(type: "int", nullable: false),
                    BufferAfterMinutes = table.Column<int>(type: "int", nullable: false),
                    DefaultKind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppointmentTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppointmentTypes_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BookingConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OnlineBookingEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AllowNewPatients = table.Column<bool>(type: "bit", nullable: false),
                    AllowReturningPatients = table.Column<bool>(type: "bit", nullable: false),
                    AllowAnyAvailableTherapist = table.Column<bool>(type: "bit", nullable: false),
                    MinNoticeHours = table.Column<int>(type: "int", nullable: false),
                    MaxAdvanceDays = table.Column<int>(type: "int", nullable: false),
                    SlotIntervalMinutes = table.Column<int>(type: "int", nullable: false),
                    CancellationPolicy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PatientChangeCutoffHours = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingConfigurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BookingConfigurations_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DiagnosisCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsBillable = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiagnosisCodes", x => x.Id);
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
                name: "LocationClosures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartDateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndDateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocationClosures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LocationClosures_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PatientPayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ProcessorReference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FailureMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AttemptedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatientPayments_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PatientStatements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StatementDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    BalanceAtGeneration = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    GeneratedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientStatements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatientStatements_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PatientStatements_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Payers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PayerId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ElectronicPayerId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AddressLine1 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AddressLine2 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    City = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    State = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ZipCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    TimelyFilingDays = table.Column<int>(type: "int", nullable: false),
                    AuthorizationRequired = table.Column<bool>(type: "bit", nullable: false),
                    AuthorizationNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Payers_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Providers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FirstName = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Specialty = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Credentials = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LicenseNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NpiNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    OnlineBookingEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LocationSharingEnabled = table.Column<bool>(type: "bit", nullable: false),
                    Bio = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Providers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Providers_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ServicePrices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CptCode = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    HomeVisitKind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    IsHomeVisitTravelFee = table.Column<bool>(type: "bit", nullable: false),
                    DepositAmount = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServicePrices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServicePrices_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Superbills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClinicianId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CodesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    PaymentProcessorReference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Superbills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Superbills_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PatientInsurancePolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Rank = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    PlanName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MemberId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GroupNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubscriberName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubscriberDateOfBirth = table.Column<DateOnly>(type: "date", nullable: true),
                    RelationshipToSubscriber = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TerminationDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Copay = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: true),
                    CoinsurancePercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    Deductible = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    AuthorizationRequired = table.Column<bool>(type: "bit", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientInsurancePolicies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatientInsurancePolicies_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PatientInsurancePolicies_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PatientInsurancePolicies_Payers_PayerId",
                        column: x => x.PayerId,
                        principalTable: "Payers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Appointments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TherapistId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Location = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LocationDetailId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsHomeVisit = table.Column<bool>(type: "bit", nullable: false),
                    PrivateNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AppointmentTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BookingSource = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ReasonForVisit = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConfirmedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Appointments", x => x.Id);
                    table.CheckConstraint("CK_Appointment_EndsAfterStart", "[EndsAt] > [StartsAt]");
                    table.ForeignKey(
                        name: "FK_Appointments_AppointmentTypes_AppointmentTypeId",
                        column: x => x.AppointmentTypeId,
                        principalTable: "AppointmentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Appointments_Locations_LocationDetailId",
                        column: x => x.LocationDetailId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Appointments_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Appointments_Providers_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "Providers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ProviderAppointmentTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppointmentTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    CustomDurationMinutes = table.Column<int>(type: "int", nullable: true),
                    CustomPrice = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderAppointmentTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProviderAppointmentTypes_AppointmentTypes_AppointmentTypeId",
                        column: x => x.AppointmentTypeId,
                        principalTable: "AppointmentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProviderAppointmentTypes_Providers_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "Providers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProviderAvailabilities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DayOfWeek = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    EffectiveUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderAvailabilities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProviderAvailabilities_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProviderAvailabilities_Providers_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "Providers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProviderLocations",
                columns: table => new
                {
                    LocationsId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProvidersId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderLocations", x => new { x.LocationsId, x.ProvidersId });
                    table.ForeignKey(
                        name: "FK_ProviderLocations_Locations_LocationsId",
                        column: x => x.LocationsId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProviderLocations_Providers_ProvidersId",
                        column: x => x.ProvidersId,
                        principalTable: "Providers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProviderTimeOffs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StartDateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndDateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderTimeOffs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProviderTimeOffs_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProviderTimeOffs_Providers_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "Providers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Waitlists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AppointmentTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProviderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EarliestDate = table.Column<DateOnly>(type: "date", nullable: false),
                    LatestDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Waitlists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Waitlists_AppointmentTypes_AppointmentTypeId",
                        column: x => x.AppointmentTypeId,
                        principalTable: "AppointmentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Waitlists_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Waitlists_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Waitlists_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Waitlists_Providers_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "Providers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "PaymentRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SuperbillId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RecordedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    ReceivedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    PaymentProcessorReference = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentRecords_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentRecords_Superbills_SuperbillId",
                        column: x => x.SuperbillId,
                        principalTable: "Superbills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Claims",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientInsuranceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DiagnosisCodeListJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ClearinghouseClaimId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Claims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Claims_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Claims_PatientInsurancePolicies_PatientInsuranceId",
                        column: x => x.PatientInsuranceId,
                        principalTable: "PatientInsurancePolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Claims_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Claims_Payers_PayerId",
                        column: x => x.PayerId,
                        principalTable: "Payers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

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
                name: "ClaimDenials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DenialCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DenialReason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DeniedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ActionNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AppealStatus = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Resolution = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimDenials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClaimDenials_Claims_ClaimId",
                        column: x => x.ClaimId,
                        principalTable: "Claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClaimDenials_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClaimDenials_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClaimTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TransferredToClaimId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Method = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    PaymentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DenialCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DenialReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RecordedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClaimTransactions_Claims_ClaimId",
                        column: x => x.ClaimId,
                        principalTable: "Claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ClaimTransactions_Claims_TransferredToClaimId",
                        column: x => x.TransferredToClaimId,
                        principalTable: "Claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClaimTransactions_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClaimTransactions_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Charges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClinicalNoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProviderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClaimId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SuperbillId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ServiceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CptCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModifiersJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Units = table.Column<int>(type: "int", nullable: false),
                    Minutes = table.Column<int>(type: "int", nullable: true),
                    RecommendedUnits = table.Column<int>(type: "int", nullable: true),
                    UnitsOverrideReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ChargeAmount = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Charges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Charges_Claims_ClaimId",
                        column: x => x.ClaimId,
                        principalTable: "Claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Charges_ClinicalNotes_ClinicalNoteId",
                        column: x => x.ClinicalNoteId,
                        principalTable: "ClinicalNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Charges_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Charges_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Charges_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Charges_Superbills_SuperbillId",
                        column: x => x.SuperbillId,
                        principalTable: "Superbills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
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

            migrationBuilder.CreateTable(
                name: "ChargeDiagnosisCodes",
                columns: table => new
                {
                    ChargeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DiagnosisCodesId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChargeDiagnosisCodes", x => new { x.ChargeId, x.DiagnosisCodesId });
                    table.ForeignKey(
                        name: "FK_ChargeDiagnosisCodes_Charges_ChargeId",
                        column: x => x.ChargeId,
                        principalTable: "Charges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChargeDiagnosisCodes_DiagnosisCodes_DiagnosisCodesId",
                        column: x => x.DiagnosisCodesId,
                        principalTable: "DiagnosisCodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_AppointmentTypeId",
                table: "Appointments",
                column: "AppointmentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_LocationDetailId_StartsAt",
                table: "Appointments",
                columns: new[] { "LocationDetailId", "StartsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_PatientId_StartsAt",
                table: "Appointments",
                columns: new[] { "PatientId", "StartsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_ProviderId_StartsAt",
                table: "Appointments",
                columns: new[] { "ProviderId", "StartsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_TherapistId_StartsAt",
                table: "Appointments",
                columns: new[] { "TherapistId", "StartsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentTypes_OrganizationId_Name",
                table: "AppointmentTypes",
                columns: new[] { "OrganizationId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BookingConfigurations_OrganizationId",
                table: "BookingConfigurations",
                column: "OrganizationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChargeDiagnosisCodes_DiagnosisCodesId",
                table: "ChargeDiagnosisCodes",
                column: "DiagnosisCodesId");

            migrationBuilder.CreateIndex(
                name: "IX_Charges_ClaimId",
                table: "Charges",
                column: "ClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_Charges_ClinicalNoteId",
                table: "Charges",
                column: "ClinicalNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_Charges_LocationId",
                table: "Charges",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Charges_OrganizationId_PatientId_ServiceDate",
                table: "Charges",
                columns: new[] { "OrganizationId", "PatientId", "ServiceDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Charges_PatientId",
                table: "Charges",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_Charges_SuperbillId",
                table: "Charges",
                column: "SuperbillId");

            migrationBuilder.CreateIndex(
                name: "IX_ClaimDenials_ClaimId",
                table: "ClaimDenials",
                column: "ClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_ClaimDenials_OrganizationId_Resolution_DueDate",
                table: "ClaimDenials",
                columns: new[] { "OrganizationId", "Resolution", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ClaimDenials_PatientId",
                table: "ClaimDenials",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_Claims_OrganizationId_PatientId_Status",
                table: "Claims",
                columns: new[] { "OrganizationId", "PatientId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Claims_PatientId",
                table: "Claims",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_Claims_PatientInsuranceId",
                table: "Claims",
                column: "PatientInsuranceId");

            migrationBuilder.CreateIndex(
                name: "IX_Claims_PayerId",
                table: "Claims",
                column: "PayerId");

            migrationBuilder.CreateIndex(
                name: "IX_ClaimTransactions_ClaimId",
                table: "ClaimTransactions",
                column: "ClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_ClaimTransactions_OrganizationId_PatientId_PaymentDate",
                table: "ClaimTransactions",
                columns: new[] { "OrganizationId", "PatientId", "PaymentDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ClaimTransactions_PatientId",
                table: "ClaimTransactions",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_ClaimTransactions_TransferredToClaimId",
                table: "ClaimTransactions",
                column: "TransferredToClaimId");

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
                name: "IX_DiagnosisCodes_Code",
                table: "DiagnosisCodes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FunctionalGoals_PatientId_Status_TargetDate",
                table: "FunctionalGoals",
                columns: new[] { "PatientId", "Status", "TargetDate" });

            migrationBuilder.CreateIndex(
                name: "IX_LocationClosures_LocationId_StartDateTime_EndDateTime",
                table: "LocationClosures",
                columns: new[] { "LocationId", "StartDateTime", "EndDateTime" });

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

            migrationBuilder.CreateIndex(
                name: "IX_PatientInsurancePolicies_OrganizationId_PatientId_Rank",
                table: "PatientInsurancePolicies",
                columns: new[] { "OrganizationId", "PatientId", "Rank" });

            migrationBuilder.CreateIndex(
                name: "IX_PatientInsurancePolicies_PatientId",
                table: "PatientInsurancePolicies",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientInsurancePolicies_PayerId",
                table: "PatientInsurancePolicies",
                column: "PayerId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientPayments_PatientId_AttemptedAt",
                table: "PatientPayments",
                columns: new[] { "PatientId", "AttemptedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PatientStatements_OrganizationId_PatientId_StatementDate",
                table: "PatientStatements",
                columns: new[] { "OrganizationId", "PatientId", "StatementDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PatientStatements_PatientId",
                table: "PatientStatements",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_Payers_OrganizationId_Name",
                table: "Payers",
                columns: new[] { "OrganizationId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRecords_PatientId_ReceivedOn",
                table: "PaymentRecords",
                columns: new[] { "PatientId", "ReceivedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRecords_SuperbillId",
                table: "PaymentRecords",
                column: "SuperbillId");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderAppointmentTypes_AppointmentTypeId",
                table: "ProviderAppointmentTypes",
                column: "AppointmentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderAppointmentTypes_ProviderId_AppointmentTypeId",
                table: "ProviderAppointmentTypes",
                columns: new[] { "ProviderId", "AppointmentTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProviderAvailabilities_LocationId",
                table: "ProviderAvailabilities",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderAvailabilities_ProviderId_LocationId_DayOfWeek",
                table: "ProviderAvailabilities",
                columns: new[] { "ProviderId", "LocationId", "DayOfWeek" });

            migrationBuilder.CreateIndex(
                name: "IX_ProviderLocations_ProvidersId",
                table: "ProviderLocations",
                column: "ProvidersId");

            migrationBuilder.CreateIndex(
                name: "IX_Providers_OrganizationId_LastName_FirstName",
                table: "Providers",
                columns: new[] { "OrganizationId", "LastName", "FirstName" });

            migrationBuilder.CreateIndex(
                name: "IX_ProviderTimeOffs_LocationId",
                table: "ProviderTimeOffs",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderTimeOffs_ProviderId_StartDateTime_EndDateTime",
                table: "ProviderTimeOffs",
                columns: new[] { "ProviderId", "StartDateTime", "EndDateTime" });

            migrationBuilder.CreateIndex(
                name: "IX_ServicePrices_OrganizationId_CptCode",
                table: "ServicePrices",
                columns: new[] { "OrganizationId", "CptCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServicePrices_OrganizationId_HomeVisitKind",
                table: "ServicePrices",
                columns: new[] { "OrganizationId", "HomeVisitKind" },
                unique: true,
                filter: "[HomeVisitKind] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ServicePrices_OrganizationId_IsHomeVisitTravelFee",
                table: "ServicePrices",
                columns: new[] { "OrganizationId", "IsHomeVisitTravelFee" },
                unique: true,
                filter: "[IsHomeVisitTravelFee] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Superbills_PatientId",
                table: "Superbills",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_Waitlists_AppointmentTypeId",
                table: "Waitlists",
                column: "AppointmentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Waitlists_LocationId",
                table: "Waitlists",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Waitlists_OrganizationId_Status",
                table: "Waitlists",
                columns: new[] { "OrganizationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Waitlists_PatientId",
                table: "Waitlists",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_Waitlists_ProviderId",
                table: "Waitlists",
                column: "ProviderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BookingConfigurations");

            migrationBuilder.DropTable(
                name: "ChargeDiagnosisCodes");

            migrationBuilder.DropTable(
                name: "ClaimDenials");

            migrationBuilder.DropTable(
                name: "ClaimTransactions");

            migrationBuilder.DropTable(
                name: "FunctionalGoals");

            migrationBuilder.DropTable(
                name: "LocationClosures");

            migrationBuilder.DropTable(
                name: "NoteAddenda");

            migrationBuilder.DropTable(
                name: "NoteInterventions");

            migrationBuilder.DropTable(
                name: "OutcomeScores");

            migrationBuilder.DropTable(
                name: "PatientPayments");

            migrationBuilder.DropTable(
                name: "PatientStatements");

            migrationBuilder.DropTable(
                name: "PaymentRecords");

            migrationBuilder.DropTable(
                name: "ProviderAppointmentTypes");

            migrationBuilder.DropTable(
                name: "ProviderAvailabilities");

            migrationBuilder.DropTable(
                name: "ProviderLocations");

            migrationBuilder.DropTable(
                name: "ProviderTimeOffs");

            migrationBuilder.DropTable(
                name: "ServicePrices");

            migrationBuilder.DropTable(
                name: "Waitlists");

            migrationBuilder.DropTable(
                name: "Charges");

            migrationBuilder.DropTable(
                name: "DiagnosisCodes");

            migrationBuilder.DropTable(
                name: "Claims");

            migrationBuilder.DropTable(
                name: "ClinicalNotes");

            migrationBuilder.DropTable(
                name: "Superbills");

            migrationBuilder.DropTable(
                name: "PatientInsurancePolicies");

            migrationBuilder.DropTable(
                name: "Appointments");

            migrationBuilder.DropTable(
                name: "Payers");

            migrationBuilder.DropTable(
                name: "AppointmentTypes");

            migrationBuilder.DropTable(
                name: "Providers");
        }
    }
}
