using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Clinical;
using PhysioTrac.Application.Common;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;
using PhysioTrac.Infrastructure.Services;

namespace PhysioTrac.Tests;

/// <summary>Phase 5B: plan-of-care certification, pull-forward, and
/// progress-note-due computation -- the three new IClinicalNoteService
/// members added on top of the already-tested Phase 5A lifecycle.</summary>
public class PtNoteTypesPhase5BTests
{
    private static (PhysioTracDbContext Db, ClinicalNoteService Service, Organization Org, Patient Patient)
        NewService(Action<Organization>? configureOrg = null, Guid? assignedTherapistId = null)
    {
        var db = new PhysioTracDbContext(
            new DbContextOptionsBuilder<PhysioTracDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        var org = new Organization { Name = "Client A", Slug = "client-a", ClientNumber = 1000, PtaCosignRequired = false };
        configureOrg?.Invoke(org);
        // AssignedTherapistId matters only for the RequirePatientAccessAsync-
        // gated reads (pull-forward, progress-note-status) -- a Therapist-role
        // caller only sees their own caseload (TenantAccessService.PatientsFor).
        var patient = new Patient { OrganizationId = org.Id, FirstName = "Pat", LastName = "Patient", DateOfBirth = new DateOnly(1990, 1, 1), AssignedTherapistId = assignedTherapistId };
        db.Organizations.Add(org);
        db.Patients.Add(patient);
        db.SaveChanges();

        var audit = new AuditService(db);
        var tenantAccess = new TenantAccessService(db, audit);
        var service = new ClinicalNoteService(db, tenantAccess, audit);
        return (db, service, org, patient);
    }

    private static TestCurrentUser Therapist(Guid orgId, Guid? userId = null) => new() { UserId = userId ?? Guid.NewGuid(), OrganizationId = orgId, Role = UserRole.Therapist };
    private static TestCurrentUser Admin(Guid orgId) => new() { UserId = Guid.NewGuid(), OrganizationId = orgId, Role = UserRole.Admin };

    private static CreateNoteRequest NoteRequest(Guid patientId, NoteType type, DateOnly serviceDate) => new(
        patientId, type, serviceDate, null,
        "Subjective", "Objective", "Interventions", "Assessment", "Plan", null, null, null, null, null);

    private static async Task<ReferringProvider> AddReferringProviderAsync(PhysioTracDbContext db, Guid orgId)
    {
        var provider = new ReferringProvider { OrganizationId = orgId, FirstName = "Nadia", LastName = "Farouk" };
        db.ReferringProviders.Add(provider);
        await db.SaveChangesAsync();
        return provider;
    }

    [Fact]
    public async Task CertifyPlanOfCare_OnASignedNote_SetsCertificationFields_DespiteImmutabilityGuard()
    {
        var (db, service, org, patient) = NewService();
        var therapistId = Guid.NewGuid();
        var therapist = Therapist(org.Id, therapistId);
        var admin = Admin(org.Id);
        var provider = await AddReferringProviderAsync(db, org.Id);

        var note = await service.CreateDraftAsync(NoteRequest(patient.Id, NoteType.PlanOfCare, DateOnly.FromDateTime(DateTime.UtcNow)), therapist);
        await service.SignNoteAsync(note.Id, true, null, therapist);

        var certifiedDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var certified = await service.CertifyPlanOfCareAsync(
            note.Id, new CertifyPlanOfCareRequest(certifiedDate, provider.Id), admin);

        Assert.Equal(certifiedDate, certified.PlanOfCareCertifiedDate);
        Assert.Equal(provider.Id, certified.PlanOfCareCertifyingProviderId);
        Assert.Equal(NoteStatus.Signed, certified.Status); // certifying never changes note status
    }

    [Fact]
    public async Task CertifyPlanOfCare_ByTherapistWhoDoesNotOwnTheNote_ThrowsForbidden()
    {
        var (db, service, org, patient) = NewService();
        var author = Therapist(org.Id);
        var otherTherapist = Therapist(org.Id);
        var provider = await AddReferringProviderAsync(db, org.Id);

        var note = await service.CreateDraftAsync(NoteRequest(patient.Id, NoteType.PlanOfCare, DateOnly.FromDateTime(DateTime.UtcNow)), author);
        await service.SignNoteAsync(note.Id, true, null, author);

        await Assert.ThrowsAsync<ForbiddenException>(() => service.CertifyPlanOfCareAsync(
            note.Id, new CertifyPlanOfCareRequest(DateOnly.FromDateTime(DateTime.UtcNow), provider.Id), otherTherapist));
    }

    [Fact]
    public async Task CertifyPlanOfCare_UnknownProvider_ThrowsNotFound()
    {
        var (db, service, org, patient) = NewService();
        var therapist = Therapist(org.Id);
        var admin = Admin(org.Id);

        var note = await service.CreateDraftAsync(NoteRequest(patient.Id, NoteType.PlanOfCare, DateOnly.FromDateTime(DateTime.UtcNow)), therapist);
        await service.SignNoteAsync(note.Id, true, null, therapist);

        await Assert.ThrowsAsync<NotFoundException>(() => service.CertifyPlanOfCareAsync(
            note.Id, new CertifyPlanOfCareRequest(DateOnly.FromDateTime(DateTime.UtcNow), Guid.NewGuid()), admin));
    }

    [Fact]
    public async Task CertifyPlanOfCare_AnotherOrganizationsNote_ThrowsNotFound()
    {
        var (db, service, org, patient) = NewService();
        var therapist = Therapist(org.Id);
        var note = await service.CreateDraftAsync(NoteRequest(patient.Id, NoteType.PlanOfCare, DateOnly.FromDateTime(DateTime.UtcNow)), therapist);
        await service.SignNoteAsync(note.Id, true, null, therapist);

        var otherOrg = new Organization { Name = "Client B", Slug = "client-b", ClientNumber = 1001 };
        db.Organizations.Add(otherOrg);
        await db.SaveChangesAsync();
        var otherOrgAdmin = Admin(otherOrg.Id);

        await Assert.ThrowsAsync<NotFoundException>(() => service.CertifyPlanOfCareAsync(
            note.Id, new CertifyPlanOfCareRequest(DateOnly.FromDateTime(DateTime.UtcNow), Guid.NewGuid()), otherOrgAdmin));
    }

    [Fact]
    public async Task GetPullForwardData_ReturnsActiveGoals_LastSignedNotesMeasurements_AndUnresolvedDiagnosesOnly()
    {
        var therapistUserId = Guid.NewGuid();
        var (db, service, org, patient) = NewService(assignedTherapistId: therapistUserId);
        var therapist = Therapist(org.Id, therapistUserId);

        db.FunctionalGoals.AddRange(
            new FunctionalGoal { PatientId = patient.Id, AuthorId = therapist.UserId, FunctionalLimitation = "Stairs", FunctionalTask = "Climb stairs", TargetDate = DateOnly.FromDateTime(DateTime.Today), Status = GoalStatus.Active },
            new FunctionalGoal { PatientId = patient.Id, AuthorId = therapist.UserId, FunctionalLimitation = "Old goal", FunctionalTask = "Discontinued task", TargetDate = DateOnly.FromDateTime(DateTime.Today), Status = GoalStatus.Discontinued });

        var diagnosisCode = new DiagnosisCode { Code = "M54.50", Description = "Low back pain, unspecified" };
        var resolvedCode = new DiagnosisCode { Code = "S93.401A", Description = "Ankle sprain" };
        db.DiagnosisCodes.AddRange(diagnosisCode, resolvedCode);
        await db.SaveChangesAsync();

        db.PatientDiagnoses.AddRange(
            new PatientDiagnosis { PatientId = patient.Id, DiagnosisCodeId = diagnosisCode.Id, IsPrimary = true },
            new PatientDiagnosis { PatientId = patient.Id, DiagnosisCodeId = resolvedCode.Id, IsPrimary = false, ResolvedDate = DateOnly.FromDateTime(DateTime.Today) });
        await db.SaveChangesAsync();

        var draftNote = await service.CreateDraftAsync(NoteRequest(patient.Id, NoteType.Daily, DateOnly.FromDateTime(DateTime.Today)), therapist);
        var draftUpdate = await service.UpdateDraftAsync(draftNote.Id,
            new UpdateNoteRequest(null, null, null, null, null, null, null, null, null, null), therapist);
        draftUpdate.ObjectiveMeasurementsJson = """{"rom":"draft-should-not-appear"}""";
        await db.SaveChangesAsync();

        var signedNote = await service.CreateDraftAsync(NoteRequest(patient.Id, NoteType.Daily, DateOnly.FromDateTime(DateTime.Today.AddDays(-1))), therapist);
        signedNote.ObjectiveMeasurementsJson = """{"rom":"110 deg flexion"}""";
        await db.SaveChangesAsync();
        await service.SignNoteAsync(signedNote.Id, true, null, therapist);

        var data = await service.GetPullForwardDataAsync(patient.Id, therapist);

        var activeGoal = Assert.Single(data.ActiveGoals);
        Assert.Equal("Stairs", activeGoal.FunctionalLimitation);

        var activeDiagnosis = Assert.Single(data.ActiveDiagnoses);
        Assert.Equal("M54.50", activeDiagnosis.Code);

        Assert.Equal("""{"rom":"110 deg flexion"}""", data.LastObjectiveMeasurementsJson);
    }

    /// <summary>Builds an already-Signed note directly against the DbContext
    /// rather than through CreateDraftAsync/SignNoteAsync -- these
    /// progress-due tests are exercising GetProgressNoteStatusAsync's own
    /// query logic, not the (separately tested) finalization compliance
    /// gate, and that gate's own "reassessment_overdue" blocker would
    /// otherwise refuse to sign the very past-due notes these tests need to
    /// already exist. Mirrors DemoDataSeeder's identical, documented
    /// shortcut for seeded signed notes.</summary>
    private static ClinicalNote SignedNote(Guid patientId, Guid therapistId, NoteType type, DateOnly serviceDate, DateOnly? reassessmentDue = null) => new()
    {
        PatientId = patientId,
        TherapistId = therapistId,
        NoteType = type,
        Status = NoteStatus.Signed,
        ServiceDate = serviceDate,
        Objective = "Objective",
        Assessment = "Assessment",
        Plan = "Plan",
        ReassessmentDue = reassessmentDue,
        SignatureName = "Test Therapist",
        SignedAt = new DateTimeOffset(serviceDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
    };

    [Fact]
    public async Task GetProgressNoteStatus_DueByDayCount_WhenReassessmentDueHasPassed()
    {
        var therapistUserId = Guid.NewGuid();
        var (db, service, org, patient) = NewService(o => o.ProgressNoteDueDays = 30, therapistUserId);
        var therapist = Therapist(org.Id, therapistUserId);

        var evalDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-40));
        db.ClinicalNotes.Add(SignedNote(patient.Id, therapist.UserId, NoteType.Evaluation, evalDate, evalDate.AddDays(30)));
        await db.SaveChangesAsync();

        var status = await service.GetProgressNoteStatusAsync(patient.Id, therapist);

        Assert.True(status.IsDue);
        Assert.True(status.DueByDayCount);
        Assert.False(status.DueByVisitCount);
    }

    [Fact]
    public async Task GetProgressNoteStatus_DueByVisitCount_WhenEnoughSignedDailyNotesFollowTheLastProgressNote()
    {
        var therapistUserId = Guid.NewGuid();
        var (db, service, org, patient) = NewService(o => o.ProgressNoteDueVisitCount = 2, therapistUserId);
        var therapist = Therapist(org.Id, therapistUserId);

        db.ClinicalNotes.Add(SignedNote(patient.Id, therapist.UserId, NoteType.Progress, DateOnly.FromDateTime(DateTime.Today.AddDays(-15))));
        db.ClinicalNotes.AddRange(
            SignedNote(patient.Id, therapist.UserId, NoteType.Daily, DateOnly.FromDateTime(DateTime.Today.AddDays(-10))),
            SignedNote(patient.Id, therapist.UserId, NoteType.Daily, DateOnly.FromDateTime(DateTime.Today.AddDays(-4))));
        await db.SaveChangesAsync();

        var status = await service.GetProgressNoteStatusAsync(patient.Id, therapist);

        Assert.True(status.IsDue);
        Assert.True(status.DueByVisitCount);
        Assert.Equal(2, status.VisitsSinceLastProgressNote);
    }

    [Fact]
    public async Task GetProgressNoteStatus_NotDue_WhenNoThresholdIsConfigured()
    {
        var therapistUserId = Guid.NewGuid();
        var (db, service, org, patient) = NewService(assignedTherapistId: therapistUserId);
        var therapist = Therapist(org.Id, therapistUserId);

        db.ClinicalNotes.Add(SignedNote(patient.Id, therapist.UserId, NoteType.Evaluation, DateOnly.FromDateTime(DateTime.Today.AddDays(-90))));
        await db.SaveChangesAsync();

        var status = await service.GetProgressNoteStatusAsync(patient.Id, therapist);

        Assert.False(status.IsDue);
        Assert.False(status.DueByDayCount);
        Assert.False(status.DueByVisitCount);
    }
}
