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
    public DbSet<ClinicalNote> ClinicalNotes => Set<ClinicalNote>();
    public DbSet<NoteAddendum> NoteAddenda => Set<NoteAddendum>();
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
    public DbSet<PatientStatement> PatientStatements => Set<PatientStatement>();

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
        });

        builder.Entity<ClinicalNote>(e =>
        {
            e.HasIndex(n => new { n.PatientId, n.ServiceDate });
            e.HasIndex(n => new { n.TherapistId, n.ServiceDate });
            e.HasIndex(n => new { n.Status, n.ReassessmentDue });
            e.HasIndex(n => n.AppointmentId).IsUnique();
            e.Property(n => n.NoteType).HasConversion<string>().HasMaxLength(20);
            e.Property(n => n.Status).HasConversion<string>().HasMaxLength(20);
            e.HasOne(n => n.Patient).WithMany(p => p.Notes)
                .HasForeignKey(n => n.PatientId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(n => n.Appointment).WithOne()
                .HasForeignKey<ClinicalNote>(n => n.AppointmentId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<NoteAddendum>(e =>
        {
            e.HasOne(a => a.Note).WithMany(n => n.Addenda)
                .HasForeignKey(a => a.NoteId).OnDelete(DeleteBehavior.Restrict);
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
            e.Property(p => p.Amount).HasPrecision(10, 2);
            e.HasOne(p => p.Patient).WithMany()
                .HasForeignKey(p => p.PatientId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(p => p.Superbill).WithMany(s => s.Payments)
                .HasForeignKey(p => p.SuperbillId).OnDelete(DeleteBehavior.SetNull);
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
            e.HasIndex(s => new { s.OrganizationId, s.CptCode }).IsUnique();
            e.HasIndex(s => new { s.OrganizationId, s.HomeVisitKind })
                .IsUnique().HasFilter("[HomeVisitKind] IS NOT NULL");
            e.HasIndex(s => new { s.OrganizationId, s.IsHomeVisitTravelFee })
                .IsUnique().HasFilter("[IsHomeVisitTravelFee] = 1");
            e.HasOne(s => s.Organization).WithMany()
                .HasForeignKey(s => s.OrganizationId).OnDelete(DeleteBehavior.Restrict);
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
            if (originalStatus == Domain.Enums.NoteStatus.Signed)
            {
                throw new InvalidOperationException("Signed notes are immutable. Create an addendum instead.");
            }
        }
    }
}
