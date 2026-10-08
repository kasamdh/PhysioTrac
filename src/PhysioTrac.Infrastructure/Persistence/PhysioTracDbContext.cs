using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Infrastructure.Identity;

namespace PhysioTrac.Infrastructure.Persistence;

public class PhysioTracDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public PhysioTracDbContext(DbContextOptions<PhysioTracDbContext> options) : base(options) { }

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<ClientNumberSequence> ClientNumberSequences => Set<ClientNumberSequence>();
    public DbSet<ClientInvitation> ClientInvitations => Set<ClientInvitation>();
    public DbSet<PrivilegedAccessGrant> PrivilegedAccessGrants => Set<PrivilegedAccessGrant>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<Provider> Providers => Set<Provider>();
    public DbSet<AppointmentType> AppointmentTypes => Set<AppointmentType>();
    public DbSet<ProviderAppointmentType> ProviderAppointmentTypes => Set<ProviderAppointmentType>();
    public DbSet<ProviderAvailability> ProviderAvailabilities => Set<ProviderAvailability>();
    public DbSet<ProviderTimeOff> ProviderTimeOffs => Set<ProviderTimeOff>();
    public DbSet<LocationClosure> LocationClosures => Set<LocationClosure>();
    public DbSet<BookingConfiguration> BookingConfigurations => Set<BookingConfiguration>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<AppointmentStatusHistory> AppointmentStatusHistories => Set<AppointmentStatusHistory>();
    public DbSet<AppointmentSeries> AppointmentSeries => Set<AppointmentSeries>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<ClinicalNote> ClinicalNotes => Set<ClinicalNote>();
    public DbSet<NoteAddendum> NoteAddenda => Set<NoteAddendum>();
    public DbSet<ClinicalNoteVersion> ClinicalNoteVersions => Set<ClinicalNoteVersion>();
    public DbSet<ClinicalNoteTemplate> ClinicalNoteTemplates => Set<ClinicalNoteTemplate>();
    public DbSet<NoteIntervention> NoteInterventions => Set<NoteIntervention>();
    public DbSet<ClinicalNoteTemplateVersion> ClinicalNoteTemplateVersions => Set<ClinicalNoteTemplateVersion>();
    public DbSet<ClinicalNoteTemplateSection> ClinicalNoteTemplateSections => Set<ClinicalNoteTemplateSection>();
    public DbSet<ClinicalNoteTemplateField> ClinicalNoteTemplateFields => Set<ClinicalNoteTemplateField>();
    public DbSet<ClinicalNoteTemplateAppointmentType> ClinicalNoteTemplateAppointmentTypes => Set<ClinicalNoteTemplateAppointmentType>();
    public DbSet<ProviderFavorite> ProviderFavorites => Set<ProviderFavorite>();
    public DbSet<ClinicalNoteFieldValue> ClinicalNoteFieldValues => Set<ClinicalNoteFieldValue>();
    public DbSet<ClinicalNoteStatusChange> ClinicalNoteStatusChanges => Set<ClinicalNoteStatusChange>();
    public DbSet<ElectronicSignature> ElectronicSignatures => Set<ElectronicSignature>();
    public DbSet<NoteCosignRequest> NoteCosignRequests => Set<NoteCosignRequest>();
    public DbSet<NoteAttachment> NoteAttachments => Set<NoteAttachment>();
    public DbSet<PlanOfCare> PlansOfCare => Set<PlanOfCare>();
    public DbSet<PainAssessment> PainAssessments => Set<PainAssessment>();
    public DbSet<BodyChartFinding> BodyChartFindings => Set<BodyChartFinding>();
    public DbSet<ObjectiveMeasurement> ObjectiveMeasurements => Set<ObjectiveMeasurement>();
    public DbSet<SpecialTestDefinition> SpecialTestDefinitions => Set<SpecialTestDefinition>();
    public DbSet<SpecialTestResult> SpecialTestResults => Set<SpecialTestResult>();
    public DbSet<InterventionLibraryItem> InterventionLibraryItems => Set<InterventionLibraryItem>();
    public DbSet<InterventionGroup> InterventionGroups => Set<InterventionGroup>();
    public DbSet<InterventionGroupItem> InterventionGroupItems => Set<InterventionGroupItem>();
    public DbSet<FunctionalGoal> FunctionalGoals => Set<FunctionalGoal>();
    public DbSet<OutcomeScore> OutcomeScores => Set<OutcomeScore>();
    public DbSet<FunctionalGoalHistory> FunctionalGoalHistory => Set<FunctionalGoalHistory>();
    public DbSet<NoteGoalProgress> NoteGoalProgress => Set<NoteGoalProgress>();
    public DbSet<Waitlist> Waitlists => Set<Waitlist>();
    public DbSet<DiagnosisCode> DiagnosisCodes => Set<DiagnosisCode>();
    public DbSet<Payer> Payers => Set<Payer>();
    public DbSet<PatientInsurance> PatientInsurancePolicies => Set<PatientInsurance>();
    public DbSet<Charge> Charges => Set<Charge>();
    public DbSet<Claim> Claims => Set<Claim>();
    public DbSet<ClaimTransaction> ClaimTransactions => Set<ClaimTransaction>();
    public DbSet<ClaimDenial> ClaimDenials => Set<ClaimDenial>();
    public DbSet<Superbill> Superbills => Set<Superbill>();
    public DbSet<PaymentRecord> PaymentRecords => Set<PaymentRecord>();
    public DbSet<PatientPayment> PatientPayments => Set<PatientPayment>();
    public DbSet<ServicePrice> ServicePrices => Set<ServicePrice>();
    public DbSet<CptCode> CptCodes => Set<CptCode>();
    public DbSet<CptCodeMapping> CptCodeMappings => Set<CptCodeMapping>();
    public DbSet<PayerFeeScheduleItem> PayerFeeScheduleItems => Set<PayerFeeScheduleItem>();
    public DbSet<PatientStatement> PatientStatements => Set<PatientStatement>();
    public DbSet<PatientDocument> PatientDocuments => Set<PatientDocument>();
    public DbSet<Consent> Consents => Set<Consent>();
    public DbSet<HomeExerciseProgram> HomeExercisePrograms => Set<HomeExerciseProgram>();
    public DbSet<HomeExerciseItem> HomeExerciseItems => Set<HomeExerciseItem>();
    public DbSet<ConsentTemplate> ConsentTemplates => Set<ConsentTemplate>();
    public DbSet<IntakeFormTemplate> IntakeFormTemplates => Set<IntakeFormTemplate>();
    public DbSet<IntakeFormSubmission> IntakeFormSubmissions => Set<IntakeFormSubmission>();
    public DbSet<DocumentShareLink> DocumentShareLinks => Set<DocumentShareLink>();
    public DbSet<ReferringProvider> ReferringProviders => Set<ReferringProvider>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<ProviderLicense> ProviderLicenses => Set<ProviderLicense>();
    public DbSet<PatientAllergy> PatientAllergies => Set<PatientAllergy>();
    public DbSet<PatientMedication> PatientMedications => Set<PatientMedication>();
    public DbSet<PatientDiagnosis> PatientDiagnoses => Set<PatientDiagnosis>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Organization>(e =>
        {
            e.HasIndex(o => o.Slug).IsUnique();
            e.HasIndex(o => o.ClientNumber).IsUnique();
            e.Property(o => o.Name).HasMaxLength(160).IsRequired();
            e.Property(o => o.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(o => o.SubscriptionTier).HasConversion<string>().HasMaxLength(20);
            e.Property(o => o.EightMinuteRuleVariant).HasConversion<string>().HasMaxLength(24);
        });

        builder.Entity<Location>(e =>
        {
            e.HasIndex(l => new { l.OrganizationId, l.Name });
            e.HasOne(l => l.Organization).WithMany(o => o.Locations)
                .HasForeignKey(l => l.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ClientNumberSequence>(e =>
        {
            e.HasKey(c => c.Id);
            // Without this, EF Core's convention treats a numeric PK as an
            // IDENTITY column -- but this table is deliberately a single,
            // always-Id=1 row that ClientProvisioningService.NextClientNumberAsync
            // inserts explicitly (see the entity's own doc comment). An
            // IDENTITY column rejects that explicit insert with SQL Server
            // error 544 unless IDENTITY_INSERT is toggled on, which nothing
            // here does -- found live, since this path had never been
            // exercised against real SQL Server before (only the in-memory
            // provider, which doesn't enforce real IDENTITY semantics).
            e.Property(c => c.Id).ValueGeneratedNever();
        });

        builder.Entity<ClientInvitation>(e =>
        {
            e.HasIndex(c => c.TokenHash).IsUnique();
            e.HasOne(c => c.Organization).WithMany()
                .HasForeignKey(c => c.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PrivilegedAccessGrant>(e =>
        {
            e.HasIndex(p => new { p.OrganizationId, p.ActorId, p.ExpiresAt });
            e.HasOne(p => p.Organization).WithMany()
                .HasForeignKey(p => p.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<UserSession>(e =>
        {
            e.HasIndex(s => s.SessionKey).IsUnique();
            e.Property(s => s.RevokedReason).HasConversion<string>().HasMaxLength(24);
            e.HasOne(s => s.Organization).WithMany()
                .HasForeignKey(s => s.OrganizationId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Patient>(e =>
        {
            e.HasIndex(p => p.MedicalRecordNumber).IsUnique();
            e.HasIndex(p => new { p.OrganizationId, p.LastName, p.FirstName });
            e.HasIndex(p => new { p.OrganizationId, p.MedicalRecordNumber });
            e.Property(p => p.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(p => p.PreferredContactMethod).HasConversion<string>().HasMaxLength(16);
            e.HasOne(p => p.Organization).WithMany(o => o.Patients)
                .HasForeignKey(p => p.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(p => p.ReferringProvider).WithMany()
                .HasForeignKey(p => p.ReferringProviderId).OnDelete(DeleteBehavior.SetNull);
            // Restrict, not SetNull -- SQL Server rejects two SetNull/cascade
            // paths from ReferringProviders down to Patients (error 1785,
            // same class of conflict as ClaimTransactions' two FKs to Claims
            // earlier this session). ReferringProviderId keeps SetNull as
            // the original relationship; this one is Restrict instead.
            e.HasOne(p => p.PrimaryCareProvider).WithMany()
                .HasForeignKey(p => p.PrimaryCareProviderId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(p => p.PrimaryLocation).WithMany()
                .HasForeignKey(p => p.PrimaryLocationId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<ReferringProvider>(e =>
        {
            e.HasIndex(r => new { r.OrganizationId, r.LastName, r.FirstName });
            e.HasOne(r => r.Organization).WithMany()
                .HasForeignKey(r => r.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Message>(e =>
        {
            e.HasIndex(m => new { m.PatientId, m.SentAt });
            e.Property(m => m.SenderRole).HasConversion<string>().HasMaxLength(20);
            e.HasOne(m => m.Organization).WithMany()
                .HasForeignKey(m => m.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(m => m.Patient).WithMany(p => p.Messages)
                .HasForeignKey(m => m.PatientId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AuditEvent>(e =>
        {
            e.HasIndex(a => new { a.OrganizationId, a.CreatedAt });
            e.HasIndex(a => new { a.PatientId, a.CreatedAt });
            // Per-note lookups (view/update de-duplication, AI-assisted flag at signing).
            e.HasIndex(a => new { a.ObjectId, a.Action, a.CreatedAt });
            e.Property(a => a.Action).HasMaxLength(80);
            e.Property(a => a.ObjectType).HasMaxLength(80);
        });

        builder.Entity<ApplicationUser>(e =>
        {
            e.Property(u => u.Role).HasConversion<string>().HasMaxLength(24);
            e.Property(u => u.Status).HasConversion<string>().HasMaxLength(16);
        });

        builder.Entity<Provider>(e =>
        {
            e.HasIndex(p => new { p.OrganizationId, p.LastName, p.FirstName });
            e.Property(p => p.Discipline).HasConversion<string>().HasMaxLength(8);
            e.HasOne(p => p.Organization).WithMany()
                .HasForeignKey(p => p.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(p => p.Locations).WithMany(l => l.Providers)
                .UsingEntity(j => j.ToTable("ProviderLocations"));
        });

        builder.Entity<ProviderLicense>(e =>
        {
            e.HasIndex(l => new { l.ProviderId, l.State, l.LicenseNumber }).IsUnique();
            e.Property(l => l.State).HasMaxLength(2).IsFixedLength();
            e.Property(l => l.Status).HasConversion<string>().HasMaxLength(16);
            e.HasOne(l => l.Provider).WithMany(p => p.Licenses)
                .HasForeignKey(l => l.ProviderId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PatientAllergy>(e =>
        {
            e.HasIndex(a => a.PatientId);
            e.Property(a => a.Severity).HasConversion<string>().HasMaxLength(20);
            e.HasOne(a => a.Patient).WithMany(p => p.Allergies)
                .HasForeignKey(a => a.PatientId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PatientMedication>(e =>
        {
            e.HasIndex(m => m.PatientId);
            e.HasOne(m => m.Patient).WithMany(p => p.Medications)
                .HasForeignKey(m => m.PatientId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PatientDiagnosis>(e =>
        {
            e.HasIndex(d => d.PatientId);
            e.HasOne(d => d.Patient).WithMany(p => p.DiagnosisRecords)
                .HasForeignKey(d => d.PatientId).OnDelete(DeleteBehavior.Cascade);
            // Restrict, not Cascade -- DiagnosisCode is shared reference
            // data (see its own doc comment); a patient's diagnosis history
            // must never be able to delete a catalog row out from under
            // every other tenant.
            e.HasOne(d => d.DiagnosisCode).WithMany()
                .HasForeignKey(d => d.DiagnosisCodeId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<DiagnosisCode>(e =>
        {
            e.HasIndex(c => c.Code).IsUnique();
        });

        builder.Entity<AppointmentType>(e =>
        {
            e.HasIndex(a => new { a.OrganizationId, a.Name }).IsUnique();
            e.Property(a => a.DefaultKind).HasConversion<string>().HasMaxLength(20);
            e.Property(a => a.Price).HasPrecision(8, 2);
            e.HasOne(a => a.Organization).WithMany()
                .HasForeignKey(a => a.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProviderAppointmentType>(e =>
        {
            e.HasIndex(pat => new { pat.ProviderId, pat.AppointmentTypeId }).IsUnique();
            e.Property(pat => pat.CustomPrice).HasPrecision(8, 2);
            e.HasOne(pat => pat.Provider).WithMany(p => p.AppointmentTypeLinks)
                .HasForeignKey(pat => pat.ProviderId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(pat => pat.AppointmentType).WithMany()
                .HasForeignKey(pat => pat.AppointmentTypeId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ProviderAvailability>(e =>
        {
            e.HasIndex(a => new { a.ProviderId, a.LocationId, a.DayOfWeek });
            e.Property(a => a.DayOfWeek).HasConversion<string>().HasMaxLength(12);
            e.HasOne(a => a.Provider).WithMany(p => p.Availabilities)
                .HasForeignKey(a => a.ProviderId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(a => a.Location).WithMany()
                .HasForeignKey(a => a.LocationId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ProviderTimeOff>(e =>
        {
            e.HasIndex(t => new { t.ProviderId, t.StartDateTime, t.EndDateTime });
            e.Property(t => t.Reason).HasConversion<string>().HasMaxLength(16);
            e.Property(t => t.Status).HasConversion<string>().HasMaxLength(16);
            e.HasOne(t => t.Provider).WithMany()
                .HasForeignKey(t => t.ProviderId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(t => t.Location).WithMany()
                .HasForeignKey(t => t.LocationId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<LocationClosure>(e =>
        {
            e.HasIndex(c => new { c.LocationId, c.StartDateTime, c.EndDateTime });
            e.HasOne(c => c.Location).WithMany()
                .HasForeignKey(c => c.LocationId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<BookingConfiguration>(e =>
        {
            e.HasIndex(b => b.OrganizationId).IsUnique();
            e.HasOne(b => b.Organization).WithMany()
                .HasForeignKey(b => b.OrganizationId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Appointment>(e =>
        {
            e.HasIndex(a => new { a.TherapistId, a.StartsAt });
            e.HasIndex(a => new { a.PatientId, a.StartsAt });
            e.HasIndex(a => new { a.ProviderId, a.StartsAt });
            e.HasIndex(a => new { a.LocationDetailId, a.StartsAt });
            e.Property(a => a.Kind).HasConversion<string>().HasMaxLength(20);
            e.Property(a => a.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(a => a.BookingSource).HasConversion<string>().HasMaxLength(20);
            e.ToTable(t => t.HasCheckConstraint("CK_Appointment_EndsAfterStart", "[EndsAt] > [StartsAt]"));
            e.HasOne(a => a.Patient).WithMany()
                .HasForeignKey(a => a.PatientId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(a => a.Provider).WithMany(p => p.Appointments)
                .HasForeignKey(a => a.ProviderId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(a => a.LocationDetail).WithMany()
                .HasForeignKey(a => a.LocationDetailId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(a => a.AppointmentType).WithMany()
                .HasForeignKey(a => a.AppointmentTypeId).OnDelete(DeleteBehavior.SetNull);
            // Restrict, not SetNull -- Location already has one SetNull path
            // to Appointment (LocationDetailId above); a second cascading
            // path through Room would hit the same SQL Server multi-path
            // error (1785) this session has already fixed twice.
            e.HasOne(a => a.Room).WithMany()
                .HasForeignKey(a => a.RoomId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(a => a.Series).WithMany(s => s.Occurrences)
                .HasForeignKey(a => a.SeriesId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Room>(e =>
        {
            e.HasIndex(r => r.LocationId);
            e.HasOne(r => r.Location).WithMany()
                .HasForeignKey(r => r.LocationId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AppointmentStatusHistory>(e =>
        {
            e.HasIndex(h => h.AppointmentId);
            e.Property(h => h.FromStatus).HasConversion<string>().HasMaxLength(16);
            e.Property(h => h.ToStatus).HasConversion<string>().HasMaxLength(16);
            e.HasOne(h => h.Appointment).WithMany()
                .HasForeignKey(h => h.AppointmentId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AppointmentSeries>(e =>
        {
            e.Property(s => s.Kind).HasConversion<string>().HasMaxLength(20);
            e.HasOne(s => s.Organization).WithMany()
                .HasForeignKey(s => s.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(s => s.Patient).WithMany()
                .HasForeignKey(s => s.PatientId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ClinicalNote>(e =>
        {
            e.HasIndex(n => new { n.PatientId, n.ServiceDate });
            e.HasIndex(n => new { n.TherapistId, n.ServiceDate });
            e.HasIndex(n => new { n.Status, n.ReassessmentDue });
            e.HasIndex(n => n.AppointmentId).IsUnique();
            e.Property(n => n.NoteType).HasConversion<string>().HasMaxLength(30);
            e.Property(n => n.Status).HasConversion<string>().HasMaxLength(30);
            e.HasOne(n => n.Patient).WithMany(p => p.Notes)
                .HasForeignKey(n => n.PatientId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(n => n.Appointment).WithOne()
                .HasForeignKey<ClinicalNote>(n => n.AppointmentId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(n => n.PlanOfCareCertifyingProvider).WithMany()
                .HasForeignKey(n => n.PlanOfCareCertifyingProviderId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(n => n.AmendsNoteId);
            e.HasOne(n => n.AmendsNote).WithMany()
                .HasForeignKey(n => n.AmendsNoteId).OnDelete(DeleteBehavior.Restrict);
            e.Property(n => n.AmendmentReason).HasMaxLength(1000);

            // Documentation foundation: provider, template-version and
            // plan-of-care links, all Restrict -- clinical records are never
            // removed by deleting something they point at.
            e.HasIndex(n => new { n.TreatingProviderId, n.ServiceDate });
            e.HasIndex(n => new { n.Status, n.ServiceDate });
            // Many notes belong to one plan of care. Explicitly non-unique:
            // EF's convention first pairs PlanOfCare.SourceNote with this
            // navigation as one-to-one and would otherwise keep the index unique.
            e.HasIndex(n => n.PlanOfCareId).IsUnique(false);
            e.HasOne(n => n.TreatingProvider).WithMany()
                .HasForeignKey(n => n.TreatingProviderId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(n => n.SupervisingProvider).WithMany()
                .HasForeignKey(n => n.SupervisingProviderId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(n => n.TemplateVersion).WithMany()
                .HasForeignKey(n => n.TemplateVersionId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(n => n.PlanOfCare).WithMany()
                .HasForeignKey(n => n.PlanOfCareId).OnDelete(DeleteBehavior.Restrict);
            e.Property(n => n.ReturnReason).HasMaxLength(1000);
            e.Property(n => n.VoidReason).HasMaxLength(1000);
        });

        builder.Entity<ClinicalNoteTemplateVersion>(e =>
        {
            e.HasIndex(v => new { v.TemplateId, v.VersionNumber }).IsUnique();
            e.Property(v => v.ChangeSummary).HasMaxLength(500);
            e.HasOne(v => v.Template).WithMany(t => t.Versions)
                .HasForeignKey(v => v.TemplateId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ClinicalNoteTemplateSection>(e =>
        {
            e.HasIndex(s => new { s.VersionId, s.Key }).IsUnique();
            e.HasIndex(s => new { s.VersionId, s.DisplayOrder });
            e.Property(s => s.Key).HasMaxLength(80);
            e.Property(s => s.Title).HasMaxLength(200);
            e.Property(s => s.HelpText).HasMaxLength(1000);
            e.Property(s => s.Component).HasMaxLength(40);
            e.HasOne(s => s.Version).WithMany(v => v.Sections)
                .HasForeignKey(s => s.VersionId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ClinicalNoteTemplateField>(e =>
        {
            e.HasIndex(f => new { f.VersionId, f.Key }).IsUnique();
            e.HasIndex(f => new { f.SectionId, f.DisplayOrder });
            e.Property(f => f.Key).HasMaxLength(80);
            e.Property(f => f.Label).HasMaxLength(200);
            e.Property(f => f.FieldType).HasConversion<string>().HasMaxLength(30);
            e.Property(f => f.HelpText).HasMaxLength(1000);
            e.Property(f => f.Placeholder).HasMaxLength(200);
            e.Property(f => f.Unit).HasMaxLength(30);
            e.Property(f => f.NoteColumn).HasMaxLength(30);
            e.Property(f => f.ConfigJson).HasMaxLength(4000);
            e.Property(f => f.ValidationJson).HasMaxLength(1000);
            e.Property(f => f.ConditionJson).HasMaxLength(1000);
            e.HasOne(f => f.Version).WithMany(v => v.Fields)
                .HasForeignKey(f => f.VersionId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(f => f.Section).WithMany(s => s.Fields)
                .HasForeignKey(f => f.SectionId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ClinicalNoteTemplateAppointmentType>(e =>
        {
            e.HasIndex(a => new { a.TemplateId, a.AppointmentTypeId }).IsUnique();
            e.HasIndex(a => a.AppointmentTypeId);
            e.HasOne(a => a.Template).WithMany(t => t.AppointmentTypes)
                .HasForeignKey(a => a.TemplateId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(a => a.AppointmentType).WithMany()
                .HasForeignKey(a => a.AppointmentTypeId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ProviderFavorite>(e =>
        {
            e.HasIndex(f => new { f.UserId, f.ItemType, f.ItemId }).IsUnique();
            e.Property(f => f.ItemType).HasConversion<string>().HasMaxLength(30);
        });

        builder.Entity<ClinicalNoteFieldValue>(e =>
        {
            e.HasIndex(v => new { v.NoteId, v.FieldKey }).IsUnique();
            e.HasIndex(v => v.FieldId);
            e.Property(v => v.FieldKey).HasMaxLength(80);
            e.Property(v => v.ValueNumber).HasPrecision(18, 4);
            e.HasOne(v => v.Note).WithMany(n => n.FieldValues)
                .HasForeignKey(v => v.NoteId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(v => v.Field).WithMany()
                .HasForeignKey(v => v.FieldId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ClinicalNoteStatusChange>(e =>
        {
            e.HasIndex(c => new { c.NoteId, c.CreatedAt });
            e.Property(c => c.FromStatus).HasConversion<string>().HasMaxLength(30);
            e.Property(c => c.ToStatus).HasConversion<string>().HasMaxLength(30);
            e.Property(c => c.Reason).HasMaxLength(1000);
            e.HasOne(c => c.Note).WithMany(n => n.StatusChanges)
                .HasForeignKey(c => c.NoteId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ElectronicSignature>(e =>
        {
            e.HasIndex(s => new { s.NoteId, s.SignedAt });
            e.HasIndex(s => s.SignerUserId);
            e.Property(s => s.Meaning).HasConversion<string>().HasMaxLength(20);
            e.Property(s => s.SignerName).HasMaxLength(200);
            e.Property(s => s.Credentials).HasMaxLength(100);
            e.Property(s => s.Role).HasMaxLength(30);
            e.Property(s => s.DisplayTimeZone).HasMaxLength(64);
            e.Property(s => s.ContentHash).HasMaxLength(64);
            e.Property(s => s.IpAddress).HasMaxLength(64);
            e.Property(s => s.Method).HasMaxLength(20);
            e.HasOne(s => s.Note).WithMany(n => n.Signatures)
                .HasForeignKey(s => s.NoteId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<NoteCosignRequest>(e =>
        {
            e.HasIndex(r => r.NoteId);
            e.HasIndex(r => new { r.Status, r.SupervisorUserId });
            e.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(r => r.ResolutionComment).HasMaxLength(1000);
            e.HasOne(r => r.Note).WithMany()
                .HasForeignKey(r => r.NoteId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<NoteAttachment>(e =>
        {
            e.HasIndex(a => new { a.NoteId, a.PatientDocumentId }).IsUnique();
            e.Property(a => a.Caption).HasMaxLength(300);
            e.HasOne(a => a.Note).WithMany()
                .HasForeignKey(a => a.NoteId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(a => a.PatientDocument).WithMany()
                .HasForeignKey(a => a.PatientDocumentId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PainAssessment>(e =>
        {
            e.HasIndex(p => p.NoteId).IsUnique();
            e.HasIndex(p => p.PatientId);
            e.Property(p => p.Scale).HasConversion<string>().HasMaxLength(20);
            e.Property(p => p.Frequency).HasConversion<string>().HasMaxLength(20);
            e.Property(p => p.Irritability).HasConversion<string>().HasMaxLength(20);
            foreach (var rating in new[] { nameof(PainAssessment.Current), nameof(PainAssessment.Best), nameof(PainAssessment.Worst),
                nameof(PainAssessment.BeforeTreatment), nameof(PainAssessment.AfterTreatment) })
            {
                e.Property(rating).HasPrecision(6, 2);
            }
            e.Property(p => p.Location).HasMaxLength(300);
            e.Property(p => p.Qualities).HasMaxLength(300);
            e.Property(p => p.Duration).HasMaxLength(200);
            e.Property(p => p.AggravatingFactors).HasMaxLength(1000);
            e.Property(p => p.EasingFactors).HasMaxLength(1000);
            e.Property(p => p.DailyPattern).HasMaxLength(1000);
            e.Property(p => p.SleepImpact).HasMaxLength(1000);
            e.Property(p => p.FunctionalImpact).HasMaxLength(2000);
            e.HasOne(p => p.Note).WithMany()
                .HasForeignKey(p => p.NoteId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<BodyChartFinding>(e =>
        {
            e.HasIndex(f => new { f.NoteId, f.Order });
            e.HasIndex(f => f.PatientId);
            e.Property(f => f.View).HasConversion<string>().HasMaxLength(10);
            e.Property(f => f.Side).HasConversion<string>().HasMaxLength(10);
            e.Property(f => f.FindingType).HasConversion<string>().HasMaxLength(20);
            e.Property(f => f.Region).HasMaxLength(40);
            e.Property(f => f.X).HasPrecision(5, 4);
            e.Property(f => f.Y).HasPrecision(5, 4);
            e.Property(f => f.RadiatesTo).HasMaxLength(200);
            e.Property(f => f.Annotation).HasMaxLength(200);
            e.Property(f => f.Comment).HasMaxLength(1000);
            e.HasOne(f => f.Note).WithMany()
                .HasForeignKey(f => f.NoteId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ObjectiveMeasurement>(e =>
        {
            e.HasIndex(m => new { m.NoteId, m.Order });
            e.HasIndex(m => new { m.PatientId, m.Category });
            e.Property(m => m.Category).HasConversion<string>().HasMaxLength(20);
            e.Property(m => m.Side).HasConversion<string>().HasMaxLength(10);
            e.Property(m => m.Item).HasMaxLength(100);
            e.Property(m => m.Movement).HasMaxLength(100);
            e.Property(m => m.Mode).HasMaxLength(30);
            e.Property(m => m.NumericValue).HasPrecision(12, 2);
            e.Property(m => m.TextValue).HasMaxLength(100);
            e.Property(m => m.Unit).HasMaxLength(20);
            e.Property(m => m.BodyRegion).HasMaxLength(60);
            e.Property(m => m.EndFeel).HasMaxLength(40);
            e.Property(m => m.Compensation).HasMaxLength(300);
            e.Property(m => m.AssistiveDevice).HasMaxLength(100);
            e.Property(m => m.AssistanceLevel).HasMaxLength(40);
            e.Property(m => m.Surface).HasMaxLength(60);
            e.Property(m => m.Condition).HasMaxLength(100);
            e.Property(m => m.Comment).HasMaxLength(1000);
            e.HasOne(m => m.Note).WithMany()
                .HasForeignKey(m => m.NoteId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<SpecialTestDefinition>(e =>
        {
            e.HasIndex(d => d.Code).IsUnique();
            e.HasIndex(d => new { d.Specialty, d.IsActive });
            e.Property(d => d.Code).HasMaxLength(60);
            e.Property(d => d.Name).HasMaxLength(150);
            e.Property(d => d.Specialty).HasConversion<string>().HasMaxLength(30);
            e.Property(d => d.ResultKind).HasConversion<string>().HasMaxLength(20);
            e.Property(d => d.BodyRegion).HasMaxLength(60);
            e.Property(d => d.Description).HasMaxLength(1000);
            e.Property(d => d.Unit).HasMaxLength(20);
            e.Property(d => d.InterpretationGuide).HasMaxLength(1000);
            e.Property(d => d.ContraindicationWarning).HasMaxLength(1000);
        });

        builder.Entity<SpecialTestResult>(e =>
        {
            e.HasIndex(r => new { r.NoteId, r.Order });
            e.HasIndex(r => new { r.PatientId, r.TestName });
            e.Property(r => r.TestName).HasMaxLength(150);
            e.Property(r => r.Specialty).HasConversion<string>().HasMaxLength(30);
            e.Property(r => r.Side).HasConversion<string>().HasMaxLength(10);
            e.Property(r => r.Outcome).HasConversion<string>().HasMaxLength(20);
            e.Property(r => r.BodyRegion).HasMaxLength(60);
            e.Property(r => r.NumericValue).HasPrecision(12, 2);
            e.Property(r => r.Unit).HasMaxLength(20);
            e.Property(r => r.Interpretation).HasMaxLength(500);
            e.Property(r => r.Comment).HasMaxLength(1000);
            e.HasOne(r => r.Note).WithMany()
                .HasForeignKey(r => r.NoteId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.Definition).WithMany()
                .HasForeignKey(r => r.DefinitionId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PlanOfCare>(e =>
        {
            e.HasIndex(p => new { p.PatientId, p.Status });
            e.HasIndex(p => new { p.Status, p.EndDate });
            e.HasIndex(p => p.SourceNoteId);
            e.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(p => p.DischargeReason).HasConversion<string>().HasMaxLength(30);
            e.Property(p => p.TreatmentDiagnosis).HasMaxLength(2000);
            e.Property(p => p.Prognosis).HasMaxLength(2000);
            e.Property(p => p.RehabPotential).HasMaxLength(500);
            e.Property(p => p.PlannedInterventions).HasMaxLength(4000);
            e.Property(p => p.HomeProgram).HasMaxLength(4000);
            e.Property(p => p.PatientEducation).HasMaxLength(4000);
            e.Property(p => p.Referrals).HasMaxLength(2000);
            e.HasOne(p => p.Patient).WithMany()
                .HasForeignKey(p => p.PatientId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(p => p.SourceNote).WithMany()
                .HasForeignKey(p => p.SourceNoteId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(p => p.PreviousPlanOfCare).WithMany()
                .HasForeignKey(p => p.PreviousPlanOfCareId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(p => p.CertifyingProvider).WithMany()
                .HasForeignKey(p => p.CertifyingProviderId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<NoteAddendum>(e =>
        {
            e.HasOne(a => a.Note).WithMany(n => n.Addenda)
                .HasForeignKey(a => a.NoteId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ClinicalNoteVersion>(e =>
        {
            e.HasIndex(v => new { v.NoteId, v.VersionNumber }).IsUnique();
            e.HasOne(v => v.Note).WithMany(n => n.Versions)
                .HasForeignKey(v => v.NoteId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ClinicalNoteTemplate>(e =>
        {
            e.HasIndex(t => new { t.OrganizationId, t.NoteType, t.Scope, t.State, t.LocationId, t.IsActive });
            e.Property(t => t.NoteType).HasConversion<string>().HasMaxLength(30);
            e.Property(t => t.Scope).HasConversion<string>().HasMaxLength(16);
            e.Property(t => t.State).HasMaxLength(2).IsFixedLength();
            e.HasOne(t => t.Organization).WithMany()
                .HasForeignKey(t => t.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(t => t.LocationDetail).WithMany()
                .HasForeignKey(t => t.LocationId).OnDelete(DeleteBehavior.Restrict);
            e.Property(t => t.Specialty).HasConversion<string>().HasMaxLength(30);
            e.Property(t => t.TemplateKey).HasMaxLength(80);
            e.Property(t => t.Description).HasMaxLength(1000);
            e.HasIndex(t => t.TemplateKey);
        });

        builder.Entity<NoteIntervention>(e =>
        {
            e.Property(i => i.Category).HasConversion<string>().HasMaxLength(32);
            // Restrict like every other clinical record: a note's treatment
            // lines are never removed by deleting something else.
            e.HasOne(i => i.Note).WithMany(n => n.InterventionItems)
                .HasForeignKey(i => i.NoteId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(i => i.LibraryItem).WithMany()
                .HasForeignKey(i => i.LibraryItemId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(i => i.CarriedForwardFromNoteId);
            e.Property(i => i.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(i => i.Description).HasMaxLength(300);
            e.Property(i => i.BodyRegion).HasMaxLength(60);
            e.Property(i => i.CptCode).HasMaxLength(10);
            e.Property(i => i.Resistance).HasMaxLength(60);
            e.Property(i => i.Duration).HasMaxLength(60);
            e.Property(i => i.Distance).HasMaxLength(60);
            e.Property(i => i.Position).HasMaxLength(60);
            e.Property(i => i.Equipment).HasMaxLength(100);
            e.Property(i => i.AssistanceLevel).HasMaxLength(40);
            e.Property(i => i.Cueing).HasMaxLength(60);
            e.Property(i => i.Modification).HasMaxLength(200);
            e.Property(i => i.PatientResponse).HasMaxLength(1000);
            e.Property(i => i.Comment).HasMaxLength(1000);
        });

        builder.Entity<InterventionLibraryItem>(e =>
        {
            e.HasIndex(i => i.Code).IsUnique();
            e.HasIndex(i => new { i.Category, i.IsActive });
            e.Property(i => i.Code).HasMaxLength(60);
            e.Property(i => i.Name).HasMaxLength(150);
            e.Property(i => i.Category).HasConversion<string>().HasMaxLength(32);
            e.Property(i => i.CptCode).HasMaxLength(10);
            e.Property(i => i.BodyRegion).HasMaxLength(60);
            e.Property(i => i.Description).HasMaxLength(1000);
            e.Property(i => i.DefaultResistance).HasMaxLength(60);
            e.Property(i => i.DefaultDuration).HasMaxLength(60);
            e.Property(i => i.DefaultEquipment).HasMaxLength(100);
            e.Property(i => i.DefaultPosition).HasMaxLength(60);
        });

        builder.Entity<InterventionGroup>(e =>
        {
            e.HasIndex(g => new { g.OwnerUserId, g.IsActive });
            e.Property(g => g.Name).HasMaxLength(120);
            e.Property(g => g.Description).HasMaxLength(500);
        });

        builder.Entity<InterventionGroupItem>(e =>
        {
            e.HasIndex(i => new { i.GroupId, i.Order });
            e.Property(i => i.Name).HasMaxLength(300);
            e.Property(i => i.Category).HasConversion<string>().HasMaxLength(32);
            e.Property(i => i.CptCode).HasMaxLength(10);
            e.Property(i => i.Resistance).HasMaxLength(60);
            e.Property(i => i.Duration).HasMaxLength(60);
            e.Property(i => i.Equipment).HasMaxLength(100);
            e.Property(i => i.Position).HasMaxLength(60);
            e.HasOne(i => i.Group).WithMany(g => g.Items)
                .HasForeignKey(i => i.GroupId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(i => i.LibraryItem).WithMany()
                .HasForeignKey(i => i.LibraryItemId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<FunctionalGoal>(e =>
        {
            e.HasIndex(g => new { g.PatientId, g.Status, g.TargetDate });
            e.Property(g => g.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(g => g.Term).HasConversion<string>().HasMaxLength(16);
            e.Property(g => g.BaselineValue).HasPrecision(8, 2);
            e.Property(g => g.TargetValue).HasPrecision(8, 2);
            e.Property(g => g.CurrentValue).HasPrecision(8, 2);
            e.Property(g => g.Comments).HasMaxLength(2000);
            e.HasOne(g => g.Patient).WithMany(p => p.Goals)
                .HasForeignKey(g => g.PatientId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<FunctionalGoalHistory>(e =>
        {
            e.ToTable("FunctionalGoalHistory");
            e.HasIndex(h => new { h.GoalId, h.CreatedAt });
            e.HasIndex(h => h.NoteId);
            e.Property(h => h.Kind).HasConversion<string>().HasMaxLength(16);
            e.Property(h => h.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(h => h.CurrentValue).HasPrecision(8, 2);
            e.Property(h => h.Comment).HasMaxLength(2000);
            e.HasOne(h => h.Goal).WithMany()
                .HasForeignKey(h => h.GoalId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<NoteGoalProgress>(e =>
        {
            e.ToTable("NoteGoalProgress");
            e.HasIndex(p => new { p.NoteId, p.GoalId }).IsUnique();
            e.HasIndex(p => p.GoalId);
            e.Property(p => p.Term).HasConversion<string>().HasMaxLength(16);
            e.Property(p => p.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(p => p.BaselineValue).HasPrecision(8, 2);
            e.Property(p => p.TargetValue).HasPrecision(8, 2);
            e.Property(p => p.PreviousValue).HasPrecision(8, 2);
            e.Property(p => p.CurrentValue).HasPrecision(8, 2);
            e.Property(p => p.Comment).HasMaxLength(2000);
            e.HasOne(p => p.Note).WithMany()
                .HasForeignKey(p => p.NoteId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(p => p.Goal).WithMany()
                .HasForeignKey(p => p.GoalId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<OutcomeScore>(e =>
        {
            e.HasIndex(o => new { o.PatientId, o.Measure, o.MeasuredOn }).IsUnique();
            e.Property(o => o.Measure).HasConversion<string>().HasMaxLength(20);
            e.Property(o => o.Score).HasPrecision(8, 2);
            e.Property(o => o.MaximumScore).HasPrecision(8, 2);
            e.Property(o => o.Interpretation).HasMaxLength(200);
            e.HasOne(o => o.Patient).WithMany(p => p.Outcomes)
                .HasForeignKey(o => o.PatientId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(o => o.Note).WithMany()
                .HasForeignKey(o => o.NoteId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Waitlist>(e =>
        {
            e.HasIndex(w => new { w.OrganizationId, w.Status });
            e.Property(w => w.Status).HasConversion<string>().HasMaxLength(16);
            e.HasOne(w => w.Organization).WithMany()
                .HasForeignKey(w => w.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(w => w.Patient).WithMany()
                .HasForeignKey(w => w.PatientId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(w => w.Location).WithMany()
                .HasForeignKey(w => w.LocationId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(w => w.AppointmentType).WithMany()
                .HasForeignKey(w => w.AppointmentTypeId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(w => w.Provider).WithMany()
                .HasForeignKey(w => w.ProviderId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<DiagnosisCode>(e =>
        {
            e.HasIndex(d => d.Code).IsUnique();
        });

        builder.Entity<Payer>(e =>
        {
            e.HasIndex(p => new { p.OrganizationId, p.Name });
            e.HasOne(p => p.Organization).WithMany()
                .HasForeignKey(p => p.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PatientInsurance>(e =>
        {
            e.HasIndex(i => new { i.OrganizationId, i.PatientId, i.Rank });
            e.Property(i => i.Rank).HasConversion<string>().HasMaxLength(16);
            e.Property(i => i.RelationshipToSubscriber).HasConversion<string>().HasMaxLength(16);
            e.Property(i => i.Copay).HasPrecision(8, 2);
            e.Property(i => i.CoinsurancePercent).HasPrecision(5, 2);
            e.Property(i => i.Deductible).HasPrecision(10, 2);
            e.HasOne(i => i.Organization).WithMany()
                .HasForeignKey(i => i.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(i => i.Patient).WithMany()
                .HasForeignKey(i => i.PatientId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(i => i.Payer).WithMany()
                .HasForeignKey(i => i.PayerId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Charge>(e =>
        {
            e.HasIndex(c => new { c.OrganizationId, c.PatientId, c.ServiceDate });
            e.Property(c => c.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(c => c.ChargeAmount).HasPrecision(10, 2);
            e.HasOne(c => c.Organization).WithMany()
                .HasForeignKey(c => c.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(c => c.Patient).WithMany()
                .HasForeignKey(c => c.PatientId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(c => c.ClinicalNote).WithMany()
                .HasForeignKey(c => c.ClinicalNoteId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(c => c.Appointment).WithMany()
                .HasForeignKey(c => c.AppointmentId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(c => c.Location).WithMany()
                .HasForeignKey(c => c.LocationId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(c => c.Claim).WithMany(cl => cl.Charges)
                .HasForeignKey(c => c.ClaimId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(c => c.Superbill).WithMany(s => s.Charges)
                .HasForeignKey(c => c.SuperbillId).OnDelete(DeleteBehavior.SetNull);
            e.HasMany(c => c.DiagnosisCodes).WithMany()
                .UsingEntity(j => j.ToTable("ChargeDiagnosisCodes"));
        });

        builder.Entity<Claim>(e =>
        {
            e.HasIndex(c => new { c.OrganizationId, c.PatientId, c.Status });
            e.Property(c => c.Status).HasConversion<string>().HasMaxLength(20);
            // DiagnosisCodeList is a convenience wrapper over DiagnosisCodeListJson
            // (the actual persisted column) â€” not a mappable collection of its own.
            e.Ignore(c => c.DiagnosisCodeList);
            e.HasOne(c => c.Organization).WithMany()
                .HasForeignKey(c => c.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(c => c.Patient).WithMany()
                .HasForeignKey(c => c.PatientId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(c => c.PatientInsurance).WithMany()
                .HasForeignKey(c => c.PatientInsuranceId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(c => c.Payer).WithMany()
                .HasForeignKey(c => c.PayerId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ClaimTransaction>(e =>
        {
            e.HasIndex(t => new { t.OrganizationId, t.PatientId, t.PaymentDate });
            e.HasIndex(t => t.ClaimId);
            e.Property(t => t.Kind).HasConversion<string>().HasMaxLength(20);
            e.Property(t => t.Method).HasConversion<string>().HasMaxLength(16);
            e.Property(t => t.Amount).HasPrecision(10, 2);
            e.HasOne(t => t.Organization).WithMany()
                .HasForeignKey(t => t.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(t => t.Patient).WithMany()
                .HasForeignKey(t => t.PatientId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(t => t.Claim).WithMany(c => c.Transactions)
                .HasForeignKey(t => t.ClaimId).OnDelete(DeleteBehavior.SetNull);
            // Restrict, not SetNull: a second cascading/nulling path to Claims
            // from the same table triggers SQL Server error 1785 ("may cause
            // cycles or multiple cascade paths") alongside the ClaimId FK
            // above. Restrict is also the more correct rule here â€” a claim
            // that is the target of a balance transfer shouldn't be
            // deletable out from under that transfer record anyway.
            e.HasOne(t => t.TransferredToClaim).WithMany()
                .HasForeignKey(t => t.TransferredToClaimId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ClaimDenial>(e =>
        {
            e.HasIndex(d => new { d.OrganizationId, d.Resolution, d.DueDate });
            e.Property(d => d.AppealStatus).HasConversion<string>().HasMaxLength(16);
            e.Property(d => d.Resolution).HasConversion<string>().HasMaxLength(24);
            e.HasOne(d => d.Organization).WithMany()
                .HasForeignKey(d => d.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(d => d.Patient).WithMany()
                .HasForeignKey(d => d.PatientId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(d => d.Claim).WithMany(c => c.Denials)
                .HasForeignKey(d => d.ClaimId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Superbill>(e =>
        {
            e.Property(s => s.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(s => s.Amount).HasPrecision(10, 2);
            e.HasOne(s => s.Patient).WithMany()
                .HasForeignKey(s => s.PatientId).OnDelete(DeleteBehavior.Restrict);
            // Codes is a convenience wrapper over CodesJson (mirrors Claim.DiagnosisCodeList).
            e.Ignore(s => s.Codes);
        });

        builder.Entity<PaymentRecord>(e =>
        {
            e.HasIndex(p => new { p.PatientId, p.ReceivedOn });
            e.Property(p => p.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(p => p.Method).HasConversion<string>().HasMaxLength(16);
            e.Property(p => p.Amount).HasPrecision(10, 2);
            e.HasOne(p => p.Patient).WithMany()
                .HasForeignKey(p => p.PatientId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(p => p.Superbill).WithMany(s => s.Payments)
                .HasForeignKey(p => p.SuperbillId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<CptCode>(e =>
        {
            e.HasIndex(c => c.Code).IsUnique();
        });

        builder.Entity<CptCodeMapping>(e =>
        {
            e.HasIndex(m => new { m.OrganizationId, m.InterventionCategory, m.IsActive });
            e.Property(m => m.InterventionCategory).HasConversion<string>().HasMaxLength(32);
            e.HasOne(m => m.Organization).WithMany()
                .HasForeignKey(m => m.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PayerFeeScheduleItem>(e =>
        {
            e.Property(f => f.AllowedAmount).HasPrecision(10, 2);
            e.HasIndex(f => new { f.PayerId, f.CptCode }).IsUnique();
            e.HasOne(f => f.Payer).WithMany()
                .HasForeignKey(f => f.PayerId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PatientPayment>(e =>
        {
            e.HasIndex(p => new { p.PatientId, p.AttemptedAt });
            e.Property(p => p.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(p => p.Amount).HasPrecision(10, 2);
            e.HasOne(p => p.Patient).WithMany()
                .HasForeignKey(p => p.PatientId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ServicePrice>(e =>
        {
            e.Property(s => s.HomeVisitKind).HasConversion<string>().HasMaxLength(20);
            e.Property(s => s.Price).HasPrecision(10, 2);
            e.Property(s => s.DepositAmount).HasPrecision(8, 2);
            // Split in two, rather than one (OrganizationId, LocationId,
            // CptCode) unique index, because EF Core's SQL Server provider
            // auto-adds a "WHERE LocationId IS NOT NULL" filter to any
            // unique index containing a nullable column (to match C#/LINQ
            // null-never-equals-null semantics) -- a single combined index
            // would then only constrain the location-specific rows, letting
            // multiple org-wide (LocationId NULL) rows for the same CPT code
            // back in. Each filtered index enforces its own scope exactly:
            // at most one org-wide default row per code, and at most one
            // row per (location, code) pair.
            e.HasIndex(s => new { s.OrganizationId, s.CptCode })
                .IsUnique().HasFilter("[LocationId] IS NULL");
            e.HasIndex(s => new { s.OrganizationId, s.LocationId, s.CptCode })
                .IsUnique().HasFilter("[LocationId] IS NOT NULL");
            e.HasIndex(s => new { s.OrganizationId, s.HomeVisitKind })
                .IsUnique().HasFilter("[HomeVisitKind] IS NOT NULL");
            e.HasIndex(s => new { s.OrganizationId, s.IsHomeVisitTravelFee })
                .IsUnique().HasFilter("[IsHomeVisitTravelFee] = 1");
            e.HasOne(s => s.Organization).WithMany()
                .HasForeignKey(s => s.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(s => s.LocationDetail).WithMany()
                .HasForeignKey(s => s.LocationId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PatientStatement>(e =>
        {
            e.HasIndex(s => new { s.OrganizationId, s.PatientId, s.StatementDate });
            e.Property(s => s.BalanceAtGeneration).HasPrecision(10, 2);
            e.HasOne(s => s.Organization).WithMany()
                .HasForeignKey(s => s.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(s => s.Patient).WithMany()
                .HasForeignKey(s => s.PatientId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PatientDocument>(e =>
        {
            e.HasIndex(d => new { d.PatientId, d.Category });
            e.HasIndex(d => d.StorageKey).IsUnique();
            e.HasOne(d => d.Organization).WithMany()
                .HasForeignKey(d => d.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(d => d.Patient).WithMany(p => p.Documents)
                .HasForeignKey(d => d.PatientId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Consent>(e =>
        {
            e.HasIndex(c => new { c.PatientId, c.ConsentType, c.SignedAt });
            e.HasOne(c => c.Organization).WithMany()
                .HasForeignKey(c => c.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(c => c.Patient).WithMany(p => p.Consents)
                .HasForeignKey(c => c.PatientId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<HomeExerciseProgram>(e =>
        {
            e.HasIndex(p => new { p.PatientId, p.Status });
            e.HasOne(p => p.Organization).WithMany()
                .HasForeignKey(p => p.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(p => p.Patient).WithMany(pt => pt.HomeExercisePrograms)
                .HasForeignKey(p => p.PatientId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<HomeExerciseItem>(e =>
        {
            e.HasOne(i => i.Program).WithMany(p => p.Items)
                .HasForeignKey(i => i.ProgramId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ConsentTemplate>(e =>
        {
            e.HasIndex(t => new { t.OrganizationId, t.ConsentType, t.Scope, t.State, t.LocationId, t.IsActive });
            e.Property(t => t.ConsentType).HasConversion<string>().HasMaxLength(24);
            e.Property(t => t.Scope).HasConversion<string>().HasMaxLength(16);
            e.Property(t => t.State).HasMaxLength(2).IsFixedLength();
            e.HasOne(t => t.Organization).WithMany()
                .HasForeignKey(t => t.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(t => t.LocationDetail).WithMany()
                .HasForeignKey(t => t.LocationId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<IntakeFormTemplate>(e =>
        {
            e.HasIndex(t => new { t.OrganizationId, t.Key, t.Scope, t.State, t.LocationId, t.IsActive });
            e.Property(t => t.Key).HasMaxLength(64);
            e.Property(t => t.Scope).HasConversion<string>().HasMaxLength(16);
            e.Property(t => t.State).HasMaxLength(2).IsFixedLength();
            e.HasOne(t => t.Organization).WithMany()
                .HasForeignKey(t => t.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(t => t.LocationDetail).WithMany()
                .HasForeignKey(t => t.LocationId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<IntakeFormSubmission>(e =>
        {
            e.HasIndex(s => new { s.PatientId, s.SubmittedAt });
            e.Property(s => s.Status).HasConversion<string>().HasMaxLength(16);
            e.HasOne(s => s.Patient).WithMany()
                .HasForeignKey(s => s.PatientId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(s => s.IntakeFormTemplate).WithMany()
                .HasForeignKey(s => s.IntakeFormTemplateId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<DocumentShareLink>(e =>
        {
            e.HasIndex(l => l.TokenHash).IsUnique();
            e.HasOne(l => l.PatientDocument).WithMany()
                .HasForeignKey(l => l.PatientDocumentId).OnDelete(DeleteBehavior.Cascade);
        });
    }

    /// <summary>Enforces append-only semantics on <see cref="AuditEvent"/> at
    /// the persistence boundary â€” mirrors the Django model's `save()`/
    /// `delete()` overrides that raise on any attempted mutation of an
    /// existing row.</summary>
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnforceAuditEventAppendOnly();
        EnforceSignedNoteImmutability();
        EnforceClinicalAppendOnly();
        EnforceNoteContentLockedAfterSigning();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        EnforceAuditEventAppendOnly();
        EnforceSignedNoteImmutability();
        EnforceClinicalAppendOnly();
        EnforceNoteContentLockedAfterSigning();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>Signatures, status history and published template versions
    /// (with their sections and fields) are history: never changed or
    /// deleted once written.</summary>
    private void EnforceClinicalAppendOnly()
    {
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Modified or EntityState.Deleted)) continue;
            var message = entry.Entity switch
            {
                ElectronicSignature => "Electronic signatures cannot be changed or deleted.",
                ClinicalNoteStatusChange => "Note status history cannot be changed or deleted.",
                Domain.Entities.FunctionalGoalHistory => "Goal history cannot be changed or deleted.",
                ClinicalNoteTemplateVersion or ClinicalNoteTemplateSection or ClinicalNoteTemplateField =>
                    "A published template version cannot be changed. Publish a new version instead.",
                _ => null,
            };
            if (message is not null) throw new InvalidOperationException(message);
        }
    }

    /// <summary>The content rows of a note (template field values,
    /// interventions, attachments -- anything INoteOwned) can only be added,
    /// changed or removed while the note is a Draft or Returned for
    /// correction. Judged by the note's status BEFORE this save, so signing
    /// and the last content save can't race each other.</summary>
    private void EnforceNoteContentLockedAfterSigning()
    {
        var noteIds = ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted && e.Entity is Domain.Common.INoteOwned)
            .Select(e => ((Domain.Common.INoteOwned)e.Entity).NoteId)
            .ToHashSet();
        if (noteIds.Count == 0) return;

        var statuses = new Dictionary<Guid, Domain.Enums.NoteStatus>();
        foreach (var noteEntry in ChangeTracker.Entries<Domain.Entities.ClinicalNote>().Where(e => noteIds.Contains(e.Entity.Id)))
        {
            // A note being created in this same save starts as a draft.
            statuses[noteEntry.Entity.Id] = noteEntry.State == EntityState.Added
                ? Domain.Enums.NoteStatus.Draft
                : (Domain.Enums.NoteStatus)noteEntry.OriginalValues[nameof(Domain.Entities.ClinicalNote.Status)]!;
        }
        var unknown = noteIds.Where(id => !statuses.ContainsKey(id)).ToList();
        if (unknown.Count > 0)
        {
            foreach (var row in ClinicalNotes.AsNoTracking().Where(n => unknown.Contains(n.Id)).Select(n => new { n.Id, n.Status }).ToList())
            {
                statuses[row.Id] = row.Status;
            }
        }

        if (statuses.Values.Any(s => s is not (Domain.Enums.NoteStatus.Draft or Domain.Enums.NoteStatus.ReturnedForCorrection)))
        {
            throw new InvalidOperationException("Signed notes are immutable. Create an addendum or amendment instead.");
        }
    }

    private void EnforceAuditEventAppendOnly()
    {
        foreach (var entry in ChangeTracker.Entries<AuditEvent>())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                throw new InvalidOperationException("Audit events are append-only and cannot be updated or deleted.");
            }
        }
    }

    /// <summary>Mirrors `ClinicalNote.save()`'s override: once a note's
    /// persisted status is Signed, no further write to that row is allowed â€”
    /// use an addendum instead. Checked against the row's ORIGINAL
    /// (pre-this-save) status, not the incoming one, so the
    /// ReviewRequiredâ†’Signed cosign transition itself is still legal.</summary>
    private void EnforceSignedNoteImmutability()
    {
        foreach (var entry in ChangeTracker.Entries<Domain.Entities.ClinicalNote>())
        {
            if (entry.State == EntityState.Deleted)
            {
                // Only an untouched draft may ever be removed; anything that
                // was submitted or signed is part of the record (void it instead).
                var deletedStatus = (Domain.Enums.NoteStatus)entry.OriginalValues[nameof(Domain.Entities.ClinicalNote.Status)]!;
                if (deletedStatus != Domain.Enums.NoteStatus.Draft)
                {
                    throw new InvalidOperationException("Clinical notes that were submitted or signed can't be deleted. Void the note instead.");
                }
                continue;
            }
            if (entry.State != EntityState.Modified) continue;
            var originalStatus = (Domain.Enums.NoteStatus)entry.OriginalValues[nameof(Domain.Entities.ClinicalNote.Status)]!;
            if (originalStatus is not (Domain.Enums.NoteStatus.Signed or Domain.Enums.NoteStatus.Locked
                or Domain.Enums.NoteStatus.Amended or Domain.Enums.NoteStatus.Voided)) continue;

            // Three legitimate writes to an already-signed row:
            // 1. Locking it (ClinicalNoteService.LockNoteAsync) -- touches
            //    only Status/UpdatedAt and only moves Signed -> Locked.
            // 2. Certifying its plan of care (CertifyPlanOfCareAsync) --
            //    touches only the two PlanOfCareCertified* fields plus
            //    UpdatedAt, at any signed/locked status. Real-world PT
            //    physician certification often happens days after the
            //    therapist's own signature; this is administrative
            //    metadata ABOUT the note, never the clinical narrative.
            // 3. Superseding it with its signed amendment
            //    (ClinicalNoteService.SignNoteAsync) -- touches only
            //    Status/UpdatedAt and only moves Signed -> Amended.
            // Anything else -- any other field touched, or Status moving
            // anywhere other than Signed -> Locked/Amended -- is exactly the
            // "signed notes are immutable" violation this guards against.
            var newStatus = (Domain.Enums.NoteStatus)entry.CurrentValues[nameof(Domain.Entities.ClinicalNote.Status)]!;
            var isLockTransition = originalStatus == Domain.Enums.NoteStatus.Signed &&
                newStatus is Domain.Enums.NoteStatus.Locked or Domain.Enums.NoteStatus.Amended;
            var modifiedNames = entry.Properties.Where(p => p.IsModified).Select(p => p.Metadata.Name).ToHashSet();

            var isLockWrite = isLockTransition &&
                modifiedNames.All(n => n is nameof(Domain.Entities.ClinicalNote.Status) or nameof(Domain.Entities.ClinicalNote.UpdatedAt)
                    or nameof(Domain.Entities.ClinicalNote.UpdatedById));
            var isCertificationWrite = originalStatus != Domain.Enums.NoteStatus.Voided && modifiedNames.All(n => n is
                nameof(Domain.Entities.ClinicalNote.PlanOfCareCertifiedDate)
                or nameof(Domain.Entities.ClinicalNote.PlanOfCareCertifyingProviderId)
                or nameof(Domain.Entities.ClinicalNote.UpdatedAt)
                or nameof(Domain.Entities.ClinicalNote.UpdatedById));
            // 4. Voiding it (ClinicalNoteService.VoidNoteAsync) -- Signed or
            //    Locked -> Voided, touching only the void fields (and
            //    releasing its appointment so the visit can be documented again).
            var isVoidWrite = originalStatus is Domain.Enums.NoteStatus.Signed or Domain.Enums.NoteStatus.Locked &&
                newStatus == Domain.Enums.NoteStatus.Voided &&
                modifiedNames.All(n => n is nameof(Domain.Entities.ClinicalNote.Status) or nameof(Domain.Entities.ClinicalNote.UpdatedAt)
                    or nameof(Domain.Entities.ClinicalNote.UpdatedById) or nameof(Domain.Entities.ClinicalNote.VoidReason)
                    or nameof(Domain.Entities.ClinicalNote.VoidedAt) or nameof(Domain.Entities.ClinicalNote.VoidedById)
                    or nameof(Domain.Entities.ClinicalNote.AppointmentId));

            if (!isLockWrite && !isCertificationWrite && !isVoidWrite)
            {
                throw new InvalidOperationException("Signed notes are immutable. Create an addendum instead.");
            }
        }
    }
}
