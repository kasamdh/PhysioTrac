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
    public DbSet<FunctionalGoal> FunctionalGoals => Set<FunctionalGoal>();
    public DbSet<OutcomeScore> OutcomeScores => Set<OutcomeScore>();
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
            e.Property(n => n.Status).HasConversion<string>().HasMaxLength(20);
            e.HasOne(n => n.Patient).WithMany(p => p.Notes)
                .HasForeignKey(n => n.PatientId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(n => n.Appointment).WithOne()
                .HasForeignKey<ClinicalNote>(n => n.AppointmentId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(n => n.PlanOfCareCertifyingProvider).WithMany()
                .HasForeignKey(n => n.PlanOfCareCertifyingProviderId).OnDelete(DeleteBehavior.SetNull);
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
        });

        builder.Entity<NoteIntervention>(e =>
        {
            e.Property(i => i.Category).HasConversion<string>().HasMaxLength(32);
            e.HasOne(i => i.Note).WithMany(n => n.InterventionItems)
                .HasForeignKey(i => i.NoteId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<FunctionalGoal>(e =>
        {
            e.HasIndex(g => new { g.PatientId, g.Status, g.TargetDate });
            e.Property(g => g.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(g => g.Term).HasConversion<string>().HasMaxLength(16);
            e.Property(g => g.BaselineValue).HasPrecision(8, 2);
            e.Property(g => g.TargetValue).HasPrecision(8, 2);
            e.Property(g => g.CurrentValue).HasPrecision(8, 2);
            e.HasOne(g => g.Patient).WithMany(p => p.Goals)
                .HasForeignKey(g => g.PatientId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<OutcomeScore>(e =>
        {
            e.HasIndex(o => new { o.PatientId, o.Measure, o.MeasuredOn }).IsUnique();
            e.Property(o => o.Measure).HasConversion<string>().HasMaxLength(20);
            e.Property(o => o.Score).HasPrecision(8, 2);
            e.Property(o => o.MaximumScore).HasPrecision(8, 2);
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
            // (the actual persisted column) — not a mappable collection of its own.
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
            // above. Restrict is also the more correct rule here — a claim
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
    /// the persistence boundary — mirrors the Django model's `save()`/
    /// `delete()` overrides that raise on any attempted mutation of an
    /// existing row.</summary>
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnforceAuditEventAppendOnly();
        EnforceSignedNoteImmutability();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        EnforceAuditEventAppendOnly();
        EnforceSignedNoteImmutability();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
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
    /// persisted status is Signed, no further write to that row is allowed —
    /// use an addendum instead. Checked against the row's ORIGINAL
    /// (pre-this-save) status, not the incoming one, so the
    /// ReviewRequired→Signed cosign transition itself is still legal.</summary>
    private void EnforceSignedNoteImmutability()
    {
        foreach (var entry in ChangeTracker.Entries<Domain.Entities.ClinicalNote>())
        {
            if (entry.State != EntityState.Modified) continue;
            var originalStatus = (Domain.Enums.NoteStatus)entry.OriginalValues[nameof(Domain.Entities.ClinicalNote.Status)]!;
            if (originalStatus is not (Domain.Enums.NoteStatus.Signed or Domain.Enums.NoteStatus.Locked)) continue;

            // Two legitimate writes to an already-Signed/Locked row:
            // 1. Locking it (ClinicalNoteService.LockNoteAsync) -- touches
            //    only Status/UpdatedAt and only moves Signed -> Locked.
            // 2. Certifying its plan of care (CertifyPlanOfCareAsync) --
            //    touches only the two PlanOfCareCertified* fields plus
            //    UpdatedAt, at any signed/locked status. Real-world PT
            //    physician certification often happens days after the
            //    therapist's own signature; this is administrative
            //    metadata ABOUT the note, never the clinical narrative.
            // Anything else -- any other field touched, or Status moving
            // anywhere other than Signed -> Locked -- is exactly the
            // "signed notes are immutable" violation this guards against.
            var newStatus = (Domain.Enums.NoteStatus)entry.CurrentValues[nameof(Domain.Entities.ClinicalNote.Status)]!;
            var isLockTransition = originalStatus == Domain.Enums.NoteStatus.Signed && newStatus == Domain.Enums.NoteStatus.Locked;
            var modifiedNames = entry.Properties.Where(p => p.IsModified).Select(p => p.Metadata.Name).ToHashSet();

            var isLockWrite = isLockTransition &&
                modifiedNames.All(n => n is nameof(Domain.Entities.ClinicalNote.Status) or nameof(Domain.Entities.ClinicalNote.UpdatedAt));
            var isCertificationWrite = modifiedNames.All(n => n is
                nameof(Domain.Entities.ClinicalNote.PlanOfCareCertifiedDate)
                or nameof(Domain.Entities.ClinicalNote.PlanOfCareCertifyingProviderId)
                or nameof(Domain.Entities.ClinicalNote.UpdatedAt));

            if (!isLockWrite && !isCertificationWrite)
            {
                throw new InvalidOperationException("Signed notes are immutable. Create an addendum instead.");
            }
        }
    }
}
