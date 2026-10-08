using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Audit;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Billing;
using PhysioTrac.Application.Clinical;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Tenancy;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Identity;
using PhysioTrac.Infrastructure.Persistence;

namespace PhysioTrac.Infrastructure.Services;

/// <summary>Direct port of `care/note_management.py`.</summary>
public partial class ClinicalNoteService : IClinicalNoteService
{
    /// <summary>Admin/Director may view, edit, sign, and co-sign any note --
    /// and, while role-based access control is off, so may every staff role.</summary>
    /// <summary>Who may finalize, lock or edit anyone's note. A plain set on
    /// purpose, NOT a <see cref="RoleSet"/>: an electronic clinical
    /// signature is a legal act, so these checks hold even while role-based
    /// access control is switched off for development (AccessControl).</summary>
    private static readonly IReadOnlySet<UserRole> FinalizingRoles = new HashSet<UserRole> { UserRole.Admin, UserRole.Director };

    private readonly PhysioTracDbContext _db;
    private readonly ITenantAccessService _tenantAccess;
    private readonly IAuditService _audit;
    private readonly ISignatureVerifier _signatureVerifier;

    public ClinicalNoteService(PhysioTracDbContext db, ITenantAccessService tenantAccess, IAuditService audit, ISignatureVerifier signatureVerifier)
    {
        _db = db;
        _tenantAccess = tenantAccess;
        _audit = audit;
        _signatureVerifier = signatureVerifier;
    }

    /// <summary>Admin/director, the author, and the clinical team (therapists
    /// and assistants in the same organization -- LoadNoteInOrgAsync already
    /// scopes every note to the caller's organization). A supervising PT
    /// must be able to read a PTA's note to cosign it, and a covering
    /// clinician needs prior notes to treat the patient.</summary>
    public bool CanViewNote(ICurrentUser user, ClinicalNote note) =>
        FinalizingRoles.Contains(user.Role) || note.TherapistId == user.UserId ||
        user.Role is UserRole.Therapist or UserRole.Assistant;

    public bool CanEditNote(ICurrentUser user, ClinicalNote note)
    {
        if (note.IsSigned) return false;
        if (FinalizingRoles.Contains(user.Role)) return true;
        return note.TherapistId == user.UserId;
    }

    public bool CanFinalizeNote(ICurrentUser user, ClinicalNote note)
    {
        if (FinalizingRoles.Contains(user.Role)) return true;
        if (note.TherapistId != user.UserId) return false;
        return CanSignNotes(user.Role) || user.Role == UserRole.Assistant;
    }

    public bool CanCosignNote(ICurrentUser user, ClinicalNote note)
    {
        if (user.UserId == note.TherapistId) return false;
        if (FinalizingRoles.Contains(user.Role)) return true;
        return user.Role == UserRole.Therapist;
    }

    /// <summary>Explicitly Status == Signed, not the broader IsSigned (which
    /// also covers Locked) -- a Locked note additionally blocks new
    /// addenda, the whole point of that further status.</summary>
    public bool CanCreateAddendum(ICurrentUser user, ClinicalNote note) =>
        note.Status == NoteStatus.Signed && CanFinalizeNote(user, note);

    public bool CanLockNote(ICurrentUser user, ClinicalNote note) =>
        note.Status == NoteStatus.Signed && FinalizingRoles.Contains(user.Role);

    public bool CanAmendNote(ICurrentUser user, ClinicalNote note) =>
        note.Status == NoteStatus.Signed && CanFinalizeNote(user, note);

    private static bool CanSignNotes(UserRole role) => role is UserRole.Admin or UserRole.Director or UserRole.Therapist;

    public async Task<ClinicalNote> CreateDraftAsync(CreateNoteRequest request, ICurrentUser actor, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, RoleSets.Clinical);
        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);

        var patient = await _db.Patients.FirstOrDefaultAsync(p => p.Id == request.PatientId && p.OrganizationId == organization.Id, ct)
            ?? throw new NotFoundException("Patient was not found.");
        if (request.AppointmentId is Guid appointmentId) await RequireAppointmentFreeAsync(appointmentId, patient.Id, request.NoteType, ct);

        var note = new ClinicalNote
        {
            PatientId = patient.Id,
            TherapistId = actor.UserId,
            AppointmentId = request.AppointmentId,
            NoteType = request.NoteType,
            Status = NoteStatus.Draft,
            ServiceDate = request.ServiceDate,
            DiagnosisSnapshot = patient.Diagnoses,
            PrecautionsSnapshot = patient.Precautions,
            Subjective = request.Subjective,
            Objective = request.Objective,
            Interventions = request.Interventions,
            Assessment = request.Assessment,
            Plan = request.Plan,
            SubjectiveDetailsJson = ChartJson.Normalize(request.SubjectiveDetailsJson, nameof(request.SubjectiveDetailsJson)) ?? "{}",
            ObjectiveMeasurementsJson = ChartJson.Normalize(request.ObjectiveMeasurementsJson, nameof(request.ObjectiveMeasurementsJson)) ?? "{}",
            PlanOfCareStart = request.PlanOfCareStart,
            PlanOfCareEnd = request.PlanOfCareEnd,
            FrequencyPerWeek = request.FrequencyPerWeek,
            DurationWeeks = request.DurationWeeks,
            // Auto-computed from the org's day-count policy when the caller
            // didn't set one explicitly and this note type actually
            // triggers a reassessment clock -- see
            // Organization.ProgressNoteDueDays's own doc comment.
            ReassessmentDue = request.ReassessmentDue ?? ComputeAutoReassessmentDue(request.NoteType, request.ServiceDate, organization),
            // Snapshotted at creation so a later org-policy change never
            // silently changes an in-progress note's cosign requirement.
            CosignRequired = organization.PtaCosignRequired && actor.Role == UserRole.Assistant,
        };
        await LinkEncounterAsync(note, actor, ct);
        note.TemplateVersionId = request.TemplateId is Guid templateId
            ? await DocumentationTemplateService.CurrentVersionIdAsync(_db, organization.Id, templateId, ct)
                ?? throw new NotFoundException("Template was not found.")
            : await DocumentationTemplateService.SuggestVersionIdAsync(_db, organization.Id, actor.UserId, note.NoteType,
                await AppointmentTypeIdAsync(note.AppointmentId, ct), ct);
        _db.ClinicalNotes.Add(note);
        RecordStatusChange(note, null, NoteStatus.Draft, actor.UserId);
        await SaveWithVersionSnapshotAsync(note, actor.UserId, isSignedVersion: false, ct);

        await _audit.RecordAuditEventAsync(actor.UserId, "note.created", nameof(ClinicalNote), note.Id, organization.Id,
            patientId: patient.Id, metadata: NoteAudit(note, organization, null), ct: ct);

        return note;
    }

    public async Task<ClinicalNote> UpdateDraftAsync(Guid noteId, UpdateNoteRequest request, ICurrentUser actor, CancellationToken ct = default)
    {
        var note = await LoadNoteInOrgAsync(noteId, actor, ct);
        if (!CanEditNote(actor, note))
        {
            throw new ForbiddenException("You are not permitted to edit this note.");
        }

        if (request.Subjective is not null) note.Subjective = request.Subjective;
        if (request.Objective is not null) note.Objective = request.Objective;
        if (request.Interventions is not null) note.Interventions = request.Interventions;
        if (request.Assessment is not null) note.Assessment = request.Assessment;
        if (request.Plan is not null) note.Plan = request.Plan;
        if (ChartJson.Normalize(request.SubjectiveDetailsJson, nameof(request.SubjectiveDetailsJson)) is string subjectiveJson)
            note.SubjectiveDetailsJson = subjectiveJson;
        if (ChartJson.Normalize(request.ObjectiveMeasurementsJson, nameof(request.ObjectiveMeasurementsJson)) is string objectiveJson)
            note.ObjectiveMeasurementsJson = objectiveJson;
        if (request.PlanOfCareStart is not null) note.PlanOfCareStart = request.PlanOfCareStart;
        if (request.PlanOfCareEnd is not null) note.PlanOfCareEnd = request.PlanOfCareEnd;
        if (request.FrequencyPerWeek is not null) note.FrequencyPerWeek = request.FrequencyPerWeek;
        if (request.DurationWeeks is not null) note.DurationWeeks = request.DurationWeeks;
        if (request.ReassessmentDue is not null) note.ReassessmentDue = request.ReassessmentDue;
        note.UpdatedAt = DateTimeOffset.UtcNow;

        // Every draft save gets its own version row -- this is what makes
        // "autosave" safe to call as often as the frontend likes: each call
        // is just another UpdateDraftAsync, and each one is a fully
        // reconstructable point in the note's history.
        var savedVersion = await SaveWithVersionSnapshotAsync(note, actor.UserId, isSignedVersion: false, ct);
        await AuditNoteOnceAsync(note, "note.updated", actor, UpdateAuditWindow, new { saveVersion = savedVersion }, ct);
        return note;
    }

    public async Task<ClinicalNote> GetAsync(Guid noteId, ICurrentUser actor, CancellationToken ct = default)
    {
        var note = await LoadNoteInOrgAsync(noteId, actor, ct);
        if (!CanViewNote(actor, note))
        {
            throw new ForbiddenException("You are not permitted to view this note.");
        }
        return note;
    }

    public async Task<IReadOnlyList<ClinicalNote>> ListForPatientAsync(Guid patientId, ICurrentUser actor, CancellationToken ct = default)
    {
        var patient = await _tenantAccess.RequirePatientAccessAsync(actor, patientId, ct: ct);
        return await _db.ClinicalNotes.Where(n => n.PatientId == patient.Id)
            .OrderByDescending(n => n.ServiceDate).ThenByDescending(n => n.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<ClinicalNote> SignNoteAsync(Guid noteId, bool attestationConfirmed, string? ipAddress, ICurrentUser actor, string? password = null,
        int? expectedSaveVersion = null, CancellationToken ct = default)
    {
        var note = await LoadNoteInOrgAsync(noteId, actor, ct);
        if (!CanFinalizeNote(actor, note))
        {
            throw new ForbiddenException("You are not permitted to finalize this note.");
        }
        if (!note.IsEditable)
        {
            throw new InvalidOperationException("Only a draft or a note returned for correction can be signed.");
        }
        if (!attestationConfirmed)
        {
            throw new InvalidOperationException("Confirm therapist review and attestation before finalizing this note.");
        }
        await RequireSaveVersionAsync(note, expectedSaveVersion, ct);
        await VerifySignerAsync(note, actor, password, "sign", ct);

        var blockers = (await ComplianceAsync(note, ct)).Where(f => f.FinalizationBlocker).ToList();
        if (blockers.Count > 0)
        {
            throw new InvalidOperationException(
                "Note cannot be finalized until required documentation checks are resolved: " +
                string.Join("; ", blockers.Select(b => b.Code == "missing_required_fields" ? $"{b.Code} ({b.Detail})" : b.Code)));
        }

        var amendedOriginal = await LoadAmendedOriginalAsync(note, ct);

        var signer = await _db.Users.FirstOrDefaultAsync(u => u.Id == actor.UserId, ct);
        note.SignatureName = DisplayName(signer, actor.UserId);
        note.SignatureCredentials = signer?.Credential;
        note.SignedAt = DateTimeOffset.UtcNow;
        note.SignatureIpAddress = ipAddress;
        note.FinalizationAttestation = true;
        // The system decides whether a PT must cosign: always for PT-only
        // note types written by an assistant, otherwise the organization's
        // policy (snapshotted on the note when it was created).
        var cosignReason = LifecycleRules.CosignReason(actor.Role, note.NoteType, note.CosignRequired);
        var pendingCosign = cosignReason is not null;
        if (pendingCosign) note.CosignRequired = true;
        var previousStatus = note.Status;
        note.Status = pendingCosign ? NoteStatus.ReviewRequired : NoteStatus.Signed;
        note.ReturnReason = null;
        if (pendingCosign) note.SubmittedAt = DateTimeOffset.UtcNow;
        note.UpdatedAt = DateTimeOffset.UtcNow;
        // Computed after every other field above is set, so the hash covers
        // the exact content -- including the signature metadata itself --
        // that becomes immutable from this point on.
        note.SignatureHash = ComputeContentHash(note, await CurrentFieldValuesAsync(note.Id, ct),
            await CurrentPainAsync(note.Id, ct), await CurrentFindingsAsync(note.Id, ct),
            await CurrentRowsAsync(_db.ObjectiveMeasurements, note.Id, ct), await CurrentRowsAsync(_db.SpecialTestResults, note.Id, ct),
            await CurrentRowsAsync(_db.NoteInterventions, note.Id, ct), await CurrentRowsAsync(_db.NoteGoalProgress, note.Id, ct),
            await NoteOutcomeRowsAsync(note.Id, ct));
        if (note.Status == NoteStatus.Signed) await ApplyFinalSignatureEffectsAsync(note, ct);
        // Same SaveChanges as the signature: the original is superseded
        // exactly when (and only if) its amendment becomes signed.
        if (amendedOriginal is not null && note.Status == NoteStatus.Signed)
        {
            RecordStatusChange(amendedOriginal, amendedOriginal.Status, NoteStatus.Amended, actor.UserId, note.AmendmentReason);
            amendedOriginal.Status = NoteStatus.Amended;
            amendedOriginal.UpdatedAt = DateTimeOffset.UtcNow;
        }

        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);
        RecordStatusChange(note, previousStatus, note.Status, actor.UserId);
        if (pendingCosign)
        {
            _db.NoteCosignRequests.Add(new NoteCosignRequest
            {
                NoteId = note.Id,
                RequestedById = actor.UserId,
                SupervisorUserId = await SupervisorUserIdAsync(note, ct),
            });
        }
        await SaveWithVersionSnapshotAsync(note, actor.UserId, isSignedVersion: true, ct, signature: new ElectronicSignature
        {
            NoteId = note.Id,
            SignerUserId = actor.UserId,
            SignerName = note.SignatureName ?? "",
            Credentials = note.SignatureCredentials,
            Role = actor.Role.ToString(),
            Meaning = note.AmendsNoteId is not null ? SignatureMeaning.Amendment : SignatureMeaning.Author,
            SignedAt = note.SignedAt.Value,
            DisplayTimeZone = organization.Timezone,
            ContentHash = note.SignatureHash,
            IpAddress = ipAddress,
        });
        await _audit.RecordAuditEventAsync(actor.UserId, pendingCosign ? "note.submitted_for_cosign" : "note.signed",
            nameof(ClinicalNote), note.Id, organization.Id, patientId: note.PatientId,
            metadata: NoteAudit(note, organization, previousStatus, new
            {
                cosignReason,
                signedVersion = await LatestVersionNumberAsync(note.Id, ct),
                // The clinician put AI-drafted text into this note (and attested to it by signing).
                aiAssisted = await _db.AuditEvents.AnyAsync(e => e.ObjectId == note.Id && e.Action == AiRules.InsertedAction, ct),
            }), ct: ct);

        if (note.Status == NoteStatus.Signed)
        {
            await AuditAmendmentAsync(note, amendedOriginal, actor, organization, ct);
            await CompleteLinkedAppointmentAsync(note, ct);
        }
        return note;
    }

    /// <summary>A further, manual step past Signed -- see NoteStatus.Locked's
    /// own doc comment. Blocked by EnforceSignedNoteImmutability from
    /// touching anything except Status/UpdatedAt, by design.</summary>
    public async Task<ClinicalNote> LockNoteAsync(Guid noteId, ICurrentUser actor, CancellationToken ct = default)
    {
        var note = await LoadNoteInOrgAsync(noteId, actor, ct);
        if (!CanLockNote(actor, note))
        {
            throw new ForbiddenException("You are not permitted to lock this note.");
        }

        RecordStatusChange(note, note.Status, NoteStatus.Locked, actor.UserId);
        note.Status = NoteStatus.Locked;
        note.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);
        await _audit.RecordAuditEventAsync(actor.UserId, "note.locked", nameof(ClinicalNote), note.Id, organization.Id,
            patientId: note.PatientId, metadata: NoteAudit(note, organization, NoteStatus.Signed), ct: ct);

        return note;
    }

    public async Task<IReadOnlyList<ClinicalNoteVersion>> GetVersionHistoryAsync(Guid noteId, ICurrentUser actor, CancellationToken ct = default)
    {
        var note = await LoadNoteInOrgAsync(noteId, actor, ct);
        if (!CanViewNote(actor, note))
        {
            throw new ForbiddenException("You are not permitted to view this note.");
        }
        return await _db.ClinicalNoteVersions.Where(v => v.NoteId == note.Id).OrderBy(v => v.VersionNumber).ToListAsync(ct);
    }

    public async Task<ClinicalNote> CertifyPlanOfCareAsync(Guid noteId, CertifyPlanOfCareRequest request, ICurrentUser actor, CancellationToken ct = default)
    {
        var note = await LoadNoteInOrgAsync(noteId, actor, ct);
        if (!CanFinalizeNote(actor, note))
        {
            throw new ForbiddenException("You are not permitted to certify this note's plan of care.");
        }

        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);
        var provider = await _db.ReferringProviders.FirstOrDefaultAsync(
            p => p.Id == request.CertifyingProviderId && p.OrganizationId == organization.Id, ct)
            ?? throw new NotFoundException("Certifying provider was not found.");

        note.PlanOfCareCertifiedDate = request.CertifiedDate;
        note.PlanOfCareCertifyingProviderId = provider.Id;
        note.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _audit.RecordAuditEventAsync(actor.UserId, "note.poc_certified", nameof(ClinicalNote), note.Id, organization.Id,
            patientId: note.PatientId, metadata: new { certifyingProviderId = provider.Id }, ct: ct);

        return note;
    }

    public async Task<PullForwardDataDto> GetPullForwardDataAsync(Guid patientId, ICurrentUser actor, CancellationToken ct = default)
    {
        var patient = await _tenantAccess.RequirePatientAccessAsync(actor, patientId, ct: ct);

        var activeGoals = await _db.FunctionalGoals
            .Where(g => g.PatientId == patient.Id && (g.Status == GoalStatus.Active || g.Status == GoalStatus.NotStarted))
            .OrderBy(g => g.TargetDate)
            .Select(g => new FunctionalGoalDto(
                g.Id, g.PatientId, g.AuthorId, g.FunctionalLimitation, g.FunctionalTask, g.Term,
                g.BaselineValue, g.TargetValue, g.CurrentValue, g.Unit, g.MeasurementMethod,
                g.TargetDate, g.Status, g.ProgressPercent, g.ApprovedById, g.ApprovedAt, g.PlanOfCareId, g.Comments, g.Version))
            .ToListAsync(ct);

        var lastNote = await _db.ClinicalNotes
            .Where(n => n.PatientId == patient.Id && (n.Status == NoteStatus.Signed || n.Status == NoteStatus.Locked))
            .OrderByDescending(n => n.ServiceDate).ThenByDescending(n => n.CreatedAt)
            .FirstOrDefaultAsync(ct);

        var activeDiagnoses = await _db.PatientDiagnoses
            .Include(d => d.DiagnosisCode)
            .Where(d => d.PatientId == patient.Id && d.ResolvedDate == null)
            .OrderByDescending(d => d.IsPrimary)
            .Select(d => new PullForwardDiagnosisDto(d.DiagnosisCodeId, d.DiagnosisCode!.Code, d.DiagnosisCode.Description, d.IsPrimary))
            .ToListAsync(ct);

        return new PullForwardDataDto(activeGoals, lastNote?.ObjectiveMeasurementsJson, activeDiagnoses);
    }

    public async Task<ProgressNoteStatusDto> GetProgressNoteStatusAsync(Guid patientId, ICurrentUser actor, CancellationToken ct = default)
    {
        var patient = await _tenantAccess.RequirePatientAccessAsync(actor, patientId, ct: ct);
        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var lastProgressTriggeringNote = await _db.ClinicalNotes
            .Where(n => n.PatientId == patient.Id && (n.Status == NoteStatus.Signed || n.Status == NoteStatus.Locked) &&
                (n.NoteType == NoteType.Evaluation || n.NoteType == NoteType.Progress || n.NoteType == NoteType.ReEvaluation))
            .OrderByDescending(n => n.ServiceDate).ThenByDescending(n => n.CreatedAt)
            .FirstOrDefaultAsync(ct);

        var dueByDayCount = lastProgressTriggeringNote?.ReassessmentDue is DateOnly due && due < today;

        var visitsSince = 0;
        if (organization.ProgressNoteDueVisitCount is int threshold)
        {
            var sinceDate = lastProgressTriggeringNote?.ServiceDate ?? DateOnly.MinValue;
            var sinceCreatedAt = lastProgressTriggeringNote?.CreatedAt ?? DateTimeOffset.MinValue;
            visitsSince = await _db.ClinicalNotes.CountAsync(n =>
                n.PatientId == patient.Id && (n.Status == NoteStatus.Signed || n.Status == NoteStatus.Locked) &&
                (n.NoteType == NoteType.Daily || n.NoteType == NoteType.Soap || n.NoteType == NoteType.HomeVisit) &&
                (n.ServiceDate > sinceDate || (n.ServiceDate == sinceDate && n.CreatedAt > sinceCreatedAt)), ct);
        }
        var dueByVisitCount = organization.ProgressNoteDueVisitCount is int t && visitsSince >= t;

        return new ProgressNoteStatusDto(
            dueByDayCount || dueByVisitCount, dueByDayCount, dueByVisitCount, visitsSince, lastProgressTriggeringNote?.ReassessmentDue);
    }

    /// <summary>Auto-computes ReassessmentDue at creation for the note types
    /// that actually start a reassessment clock, when the org has a
    /// day-count policy configured and the caller didn't set one explicitly.
    /// Visit-count due-ness isn't a calendar date and so isn't set here --
    /// see GetProgressNoteStatusAsync for that check instead.</summary>
    private static DateOnly? ComputeAutoReassessmentDue(NoteType noteType, DateOnly serviceDate, Organization organization)
    {
        if (noteType is not (NoteType.Evaluation or NoteType.Progress or NoteType.ReEvaluation)) return null;
        if (organization.ProgressNoteDueDays is not int days) return null;
        return serviceDate.AddDays(days);
    }

    public async Task<ClinicalNote> CosignNoteAsync(Guid noteId, ICurrentUser actor, string? password = null, CancellationToken ct = default)
    {
        var note = await LoadNoteInOrgAsync(noteId, actor, ct);
        if (!CanCosignNote(actor, note))
        {
            throw new ForbiddenException("You are not permitted to cosign this note.");
        }
        if (!LifecycleRules.IsAwaitingReview(note.Status))
        {
            throw new InvalidOperationException("Only a note awaiting cosign can be cosigned.");
        }
        await VerifySignerAsync(note, actor, password, "cosign", ct);
        var amendedOriginal = await LoadAmendedOriginalAsync(note, ct);

        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);
        var cosigner = await _db.Users.FirstOrDefaultAsync(u => u.Id == actor.UserId, ct);
        note.CosignedById = actor.UserId;
        note.CosignedAt = DateTimeOffset.UtcNow;
        var reviewStatus = note.Status;
        RecordStatusChange(note, note.Status, NoteStatus.Signed, actor.UserId);
        note.Status = NoteStatus.Signed;
        note.UpdatedAt = DateTimeOffset.UtcNow;
        await ApplyFinalSignatureEffectsAsync(note, ct);
        if (amendedOriginal is not null)
        {
            RecordStatusChange(amendedOriginal, amendedOriginal.Status, NoteStatus.Amended, actor.UserId, note.AmendmentReason);
            amendedOriginal.Status = NoteStatus.Amended;
            amendedOriginal.UpdatedAt = DateTimeOffset.UtcNow;
        }
        foreach (var request in await _db.NoteCosignRequests.Where(r => r.NoteId == note.Id && r.Status == CosignRequestStatus.Pending).ToListAsync(ct))
        {
            request.Status = CosignRequestStatus.Cosigned;
            request.ResolvedAt = note.CosignedAt;
            request.ResolvedById = actor.UserId;
        }
        _db.ElectronicSignatures.Add(new ElectronicSignature
        {
            NoteId = note.Id,
            SignerUserId = actor.UserId,
            SignerName = DisplayName(cosigner, actor.UserId),
            Credentials = cosigner?.Credential,
            Role = actor.Role.ToString(),
            Meaning = SignatureMeaning.Cosign,
            SignedAt = note.CosignedAt.Value,
            DisplayTimeZone = organization.Timezone,
            NoteVersionNumber = await LatestVersionNumberAsync(note.Id, ct),
            ContentHash = note.SignatureHash,
        });
        await _db.SaveChangesAsync(ct);

        await _audit.RecordAuditEventAsync(actor.UserId, "note.cosigned", nameof(ClinicalNote), note.Id, organization.Id,
            patientId: note.PatientId, metadata: NoteAudit(note, organization, reviewStatus), ct: ct);
        await AuditAmendmentAsync(note, amendedOriginal, actor, organization, ct);

        await CompleteLinkedAppointmentAsync(note, ct);
        return note;
    }

    public async Task<NoteAddendum> CreateAddendumAsync(Guid noteId, CreateAddendumRequest request, ICurrentUser actor, CancellationToken ct = default)
    {
        var note = await LoadNoteInOrgAsync(noteId, actor, ct);
        if (!CanCreateAddendum(actor, note))
        {
            throw new ForbiddenException("You are not permitted to add an addendum to this note.");
        }
        if (!note.IsSigned)
        {
            throw new InvalidOperationException("An addendum can only be created for a signed note.");
        }

        var reason = request.Reason?.Trim() ?? "";
        var body = request.Body?.Trim() ?? "";
        if (reason.Length == 0) throw new InvalidOperationException("Enter the reason for the addendum.");
        if (body.Length == 0) throw new InvalidOperationException("Enter the addendum text.");
        if (reason.Length > 500 || body.Length > 4000) throw new InvalidOperationException("The addendum is too long.");

        var addendum = new NoteAddendum { NoteId = note.Id, AuthorId = actor.UserId, Reason = reason, Body = body };
        _db.NoteAddenda.Add(addendum);
        await _db.SaveChangesAsync(ct);

        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);
        await _audit.RecordAuditEventAsync(actor.UserId, "note.addendum_created", nameof(NoteAddendum), addendum.Id, organization.Id,
            patientId: note.PatientId, metadata: new { noteId = note.Id }, ct: ct);

        return addendum;
    }

    public async Task<NoteIntervention> AddInterventionAsync(Guid noteId, CreateInterventionRequest request, ICurrentUser actor, CancellationToken ct = default)
    {
        var note = await LoadNoteInOrgAsync(noteId, actor, ct);
        if (!CanEditNote(actor, note))
        {
            throw new ForbiddenException("You are not permitted to edit this note.");
        }
        if (note.IsSigned)
        {
            throw new InvalidOperationException("Interventions cannot be changed once the note is signed.");
        }

        ValidateIntervention(request);
        var intervention = new NoteIntervention
        {
            NoteId = note.Id,
            Description = request.Description.Trim(),
            BodyRegion = request.BodyRegion,
            Category = request.Category,
            Minutes = request.Minutes,
            Units = request.Units,
            IsTimed = request.IsTimed,
            Order = request.Order,
            PatientResponse = request.PatientResponse,
        };
        _db.NoteInterventions.Add(intervention);
        await _db.SaveChangesAsync(ct);
        return intervention;
    }

    public async Task<NoteIntervention> UpdateInterventionAsync(Guid noteId, Guid interventionId, CreateInterventionRequest request, ICurrentUser actor, CancellationToken ct = default)
    {
        var (note, intervention) = await LoadEditableInterventionAsync(noteId, interventionId, actor, ct);
        ValidateIntervention(request);
        intervention.Description = request.Description.Trim();
        intervention.BodyRegion = request.BodyRegion;
        intervention.Category = request.Category;
        intervention.Minutes = request.Minutes;
        intervention.Units = request.Units;
        intervention.IsTimed = request.IsTimed;
        intervention.Order = request.Order;
        intervention.PatientResponse = request.PatientResponse;
        intervention.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return intervention;
    }

    public async Task DeleteInterventionAsync(Guid noteId, Guid interventionId, ICurrentUser actor, CancellationToken ct = default)
    {
        var (_, intervention) = await LoadEditableInterventionAsync(noteId, interventionId, actor, ct);
        _db.NoteInterventions.Remove(intervention);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<InterventionSummaryDto> SummarizeInterventionsAsync(Guid noteId, ICurrentUser actor, CancellationToken ct = default)
    {
        var items = await ListInterventionsAsync(noteId, actor, ct);
        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);
        var timed = items.Where(i => i.IsTimed).Sum(i => i.Minutes);
        return new InterventionSummaryDto(
            timed, items.Count(i => !i.IsTimed),
            EightMinuteRuleCalculator.ComputeUnits(timed, organization.EightMinuteRuleVariant),
            organization.EightMinuteRuleVariant.ToString());
    }

    private async Task<(ClinicalNote Note, NoteIntervention Intervention)> LoadEditableInterventionAsync(
        Guid noteId, Guid interventionId, ICurrentUser actor, CancellationToken ct)
    {
        var note = await LoadNoteInOrgAsync(noteId, actor, ct);
        if (!CanEditNote(actor, note))
        {
            throw new ForbiddenException("You are not permitted to edit this note.");
        }
        if (!note.IsEditable)
        {
            throw new InvalidOperationException("Interventions cannot be changed once the note is signed.");
        }
        var intervention = await _db.NoteInterventions.FirstOrDefaultAsync(i => i.Id == interventionId && i.NoteId == note.Id, ct)
            ?? throw new NotFoundException("Intervention was not found.");
        return (note, intervention);
    }

    private static void ValidateIntervention(CreateInterventionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Description))
            throw new InvalidOperationException("Describe the intervention.");
        if (request.Description.Length > 300 || (request.PatientResponse?.Length ?? 0) > 1000)
            throw new InvalidOperationException("The intervention text is too long.");
        if (request.Minutes is < 0 or > 480)
            throw new InvalidOperationException("Minutes must be between 0 and 480.");
        if (request.Units is < 0 or > 32)
            throw new InvalidOperationException("Units must be between 0 and 32.");
    }

    public async Task<IReadOnlyList<NoteIntervention>> ListInterventionsAsync(Guid noteId, ICurrentUser actor, CancellationToken ct = default)
    {
        var note = await LoadNoteInOrgAsync(noteId, actor, ct);
        if (!CanViewNote(actor, note))
        {
            throw new ForbiddenException("You are not permitted to view this note.");
        }
        return await _db.NoteInterventions.Where(i => i.NoteId == note.Id).OrderBy(i => i.Order).ThenBy(i => i.CreatedAt).ToListAsync(ct);
    }

    public async Task<ClinicalNote> CreateAmendmentAsync(Guid noteId, CreateAmendmentRequest request, ICurrentUser actor, CancellationToken ct = default)
    {
        var original = await LoadNoteInOrgAsync(noteId, actor, ct);
        if (!CanFinalizeNote(actor, original))
        {
            throw new ForbiddenException("You are not permitted to amend this note.");
        }
        if (original.Status != NoteStatus.Signed)
        {
            throw new InvalidOperationException(original.Status switch
            {
                NoteStatus.Locked => "This note is locked and can no longer be amended.",
                NoteStatus.Amended => "This note has already been amended. Amend its amendment instead.",
                _ => "Only a signed note can be amended.",
            });
        }

        // One open amendment at a time: reopening "Amend" continues it.
        var open = await _db.ClinicalNotes.FirstOrDefaultAsync(n => n.AmendsNoteId == original.Id &&
            (n.Status == NoteStatus.Draft || n.Status == NoteStatus.ReviewRequired || n.Status == NoteStatus.InReview ||
             n.Status == NoteStatus.ReturnedForCorrection), ct);
        if (open is not null) return open;

        var reason = request.Reason?.Trim() ?? "";
        if (reason.Length == 0) throw new InvalidOperationException("Enter the reason for the amendment.");
        if (reason.Length > 1000) throw new InvalidOperationException("The reason is too long.");

        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);
        var amendment = new ClinicalNote
        {
            PatientId = original.PatientId,
            TherapistId = actor.UserId,
            // The original keeps the visit link (unique per appointment); the
            // amendment points at the original instead.
            AppointmentId = null,
            AmendsNoteId = original.Id,
            AmendmentReason = reason,
            NoteType = original.NoteType,
            Status = NoteStatus.Draft,
            ServiceDate = original.ServiceDate,
            DiagnosisSnapshot = original.DiagnosisSnapshot,
            PrecautionsSnapshot = original.PrecautionsSnapshot,
            Subjective = original.Subjective,
            Objective = original.Objective,
            Interventions = original.Interventions,
            Assessment = original.Assessment,
            Plan = original.Plan,
            SubjectiveDetailsJson = original.SubjectiveDetailsJson,
            ObjectiveMeasurementsJson = original.ObjectiveMeasurementsJson,
            DischargeDetailsJson = original.DischargeDetailsJson,
            HomeVisitDetailsJson = original.HomeVisitDetailsJson,
            PlanOfCareStart = original.PlanOfCareStart,
            PlanOfCareEnd = original.PlanOfCareEnd,
            FrequencyPerWeek = original.FrequencyPerWeek,
            DurationWeeks = original.DurationWeeks,
            ReassessmentDue = original.ReassessmentDue,
            CosignRequired = organization.PtaCosignRequired && actor.Role == UserRole.Assistant,
            TemplateVersionId = original.TemplateVersionId,
            PeriodStart = original.PeriodStart,
            PeriodEnd = original.PeriodEnd,
        };
        await LinkEncounterAsync(amendment, actor, ct);
        if (await _db.PainAssessments.AsNoTracking().FirstOrDefaultAsync(p => p.NoteId == original.Id, ct) is { } originalPain)
        {
            originalPain.Id = Guid.NewGuid();
            originalPain.NoteId = amendment.Id;
            originalPain.RowVersion = [];
            _db.PainAssessments.Add(originalPain);
        }
        foreach (var m in await _db.ObjectiveMeasurements.AsNoTracking().Where(x => x.NoteId == original.Id).ToListAsync(ct))
        {
            m.Id = Guid.NewGuid();
            m.NoteId = amendment.Id;
            m.RowVersion = [];
            _db.ObjectiveMeasurements.Add(m);
        }
        foreach (var t in await _db.SpecialTestResults.AsNoTracking().Where(x => x.NoteId == original.Id).ToListAsync(ct))
        {
            t.Id = Guid.NewGuid();
            t.NoteId = amendment.Id;
            t.RowVersion = [];
            _db.SpecialTestResults.Add(t);
        }
        foreach (var finding in await _db.BodyChartFindings.AsNoTracking().Where(f => f.NoteId == original.Id).ToListAsync(ct))
        {
            finding.Id = Guid.NewGuid();
            finding.NoteId = amendment.Id;
            finding.RowVersion = [];
            _db.BodyChartFindings.Add(finding);
        }
        foreach (var value in await _db.ClinicalNoteFieldValues.AsNoTracking().Where(v => v.NoteId == original.Id).ToListAsync(ct))
        {
            _db.ClinicalNoteFieldValues.Add(new ClinicalNoteFieldValue
            {
                NoteId = amendment.Id,
                FieldId = value.FieldId,
                FieldKey = value.FieldKey,
                ValueText = value.ValueText,
                ValueNumber = value.ValueNumber,
                ValueDate = value.ValueDate,
                ValueTime = value.ValueTime,
                ValueBool = value.ValueBool,
                ValueJson = value.ValueJson,
            });
        }
        _db.ClinicalNotes.Add(amendment);
        RecordStatusChange(amendment, null, NoteStatus.Draft, actor.UserId, reason);
        foreach (var i in await _db.NoteInterventions.AsNoTracking().Where(x => x.NoteId == original.Id).ToListAsync(ct))
        {
            i.Id = Guid.NewGuid();
            i.NoteId = amendment.Id;
            i.RowVersion = [];
            _db.NoteInterventions.Add(i);
        }
        foreach (var p in await _db.NoteGoalProgress.AsNoTracking().Where(x => x.NoteId == original.Id).ToListAsync(ct))
        {
            p.Id = Guid.NewGuid();
            p.NoteId = amendment.Id;
            p.RowVersion = [];
            _db.NoteGoalProgress.Add(p);
        }
        await SaveWithVersionSnapshotAsync(amendment, actor.UserId, isSignedVersion: false, ct);

        await _audit.RecordAuditEventAsync(actor.UserId, "note.amendment_started", nameof(ClinicalNote), amendment.Id, organization.Id,
            patientId: original.PatientId, metadata: NoteAudit(amendment, organization, null, new { amendsNoteId = original.Id }), ct: ct);
        return amendment;
    }

    public async Task<NoteRecordDto> GetNoteRecordAsync(Guid noteId, ICurrentUser actor, CancellationToken ct = default)
    {
        var note = await GetAsync(noteId, actor, ct);
        var amendment = await _db.ClinicalNotes.Where(n => n.AmendsNoteId == note.Id)
            .OrderByDescending(n => n.CreatedAt).Select(n => new { n.Id, n.Status }).FirstOrDefaultAsync(ct);

        var addenda = note.Addenda.OrderBy(a => a.CreatedAt).ToList();
        var names = await NamesAsync(
            addenda.Select(a => a.AuthorId).Append(note.TherapistId).Concat(note.CosignedById is Guid c ? [c] : []), ct);

        var actions = new NoteActionsDto(
            CanEdit: CanEditNote(actor, note) && note.IsEditable,
            CanSign: note.IsEditable && CanFinalizeNote(actor, note),
            CanCosign: LifecycleRules.IsAwaitingReview(note.Status) && CanCosignNote(actor, note),
            CanAddAddendum: CanCreateAddendum(actor, note),
            CanAmend: CanAmendNote(actor, note),
            CanLock: CanLockNote(actor, note),
            CanStartReview: note.Status == NoteStatus.ReviewRequired && CanCosignNote(actor, note),
            CanReturn: LifecycleRules.IsAwaitingReview(note.Status) && CanCosignNote(actor, note),
            CanVoid: CanVoidNote(actor, note),
            SignSubmitsForCosign: LifecycleRules.CosignReason(actor.Role, note.NoteType, note.CosignRequired) is not null,
            VoidNeedsPassword: note.IsSigned);

        return new NoteRecordDto(
            actions,
            names.GetValueOrDefault(note.TherapistId, ""),
            note.CosignedById is Guid cosigner ? names.GetValueOrDefault(cosigner) : null,
            addenda.Select(a => new NoteAddendumDto(a.Id, a.NoteId, a.AuthorId, a.Reason, a.Body, a.CreatedAt,
                names.GetValueOrDefault(a.AuthorId))).ToList(),
            amendment?.Id,
            amendment?.Status);
    }

    public async Task<IReadOnlyList<ClinicalNoteVersionDto>> GetVersionHistoryViewAsync(Guid noteId, ICurrentUser actor, CancellationToken ct = default)
    {
        var versions = await GetVersionHistoryAsync(noteId, actor, ct);
        var names = await NamesAsync(versions.Select(v => v.SavedById), ct);
        return versions.Select(v => new ClinicalNoteVersionDto(v.Id, v.NoteId, v.VersionNumber, v.ContentJson, v.SavedById,
            v.IsSignedVersion, v.CreatedAt, names.GetValueOrDefault(v.SavedById))).ToList();
    }

    public async Task<NoteQueuesDto> GetNoteQueuesAsync(ICurrentUser actor, CancellationToken ct = default)
    {
        // Organization-wide on purpose: a note you wrote, or one awaiting your
        // cosignature, needs you whatever your current caseload is.
        var patients = _tenantAccess.PatientsFor(actor, clinical: false);
        var mine = await (
            from n in _db.ClinicalNotes
            join p in patients on n.PatientId equals p.Id
            where n.TherapistId == actor.UserId && (n.Status == NoteStatus.Draft || n.Status == NoteStatus.ReviewRequired ||
                n.Status == NoteStatus.InReview || n.Status == NoteStatus.ReturnedForCorrection)
            select new { n.Id, n.PatientId, p.FirstName, p.LastName, p.MedicalRecordNumber, n.NoteType, n.Status, n.ServiceDate, n.TherapistId, n.AmendsNoteId })
            .Take(200).ToListAsync(ct);

        var mayCosign = FinalizingRoles.Contains(actor.Role) || actor.Role == UserRole.Therapist;
        var toCosign = !mayCosign ? [] : await (
            from n in _db.ClinicalNotes
            join p in patients on n.PatientId equals p.Id
            where (n.Status == NoteStatus.ReviewRequired || n.Status == NoteStatus.InReview) && n.TherapistId != actor.UserId
            select new { n.Id, n.PatientId, p.FirstName, p.LastName, p.MedicalRecordNumber, n.NoteType, n.Status, n.ServiceDate, n.TherapistId, n.AmendsNoteId })
            .Take(200).ToListAsync(ct);

        var names = await NamesAsync(mine.Concat(toCosign).Select(n => n.TherapistId), ct);
        NoteQueueItemDto Row(Guid id, Guid patientId, string first, string last, string mrn, NoteType type, NoteStatus status,
            DateOnly date, Guid author, Guid? amends) =>
            new(id, patientId, $"{first} {last}".Trim(), mrn, type, status, date, names.GetValueOrDefault(author, ""), amends is not null);

        return new NoteQueuesDto(
            mine.OrderBy(n => n.ServiceDate).Select(n => Row(n.Id, n.PatientId, n.FirstName, n.LastName, n.MedicalRecordNumber,
                n.NoteType, n.Status, n.ServiceDate, n.TherapistId, n.AmendsNoteId)).ToList(),
            toCosign.OrderBy(n => n.ServiceDate).Select(n => Row(n.Id, n.PatientId, n.FirstName, n.LastName, n.MedicalRecordNumber,
                n.NoteType, n.Status, n.ServiceDate, n.TherapistId, n.AmendsNoteId)).ToList());
    }

    public async Task<NoteHistoryDto> GetNoteHistoryAsync(Guid noteId, ICurrentUser actor, CancellationToken ct = default)
    {
        var note = await GetAsync(noteId, actor, ct);
        var signatures = await _db.ElectronicSignatures.AsNoTracking().Where(s => s.NoteId == note.Id)
            .OrderBy(s => s.SignedAt).ToListAsync(ct);
        var changes = await _db.ClinicalNoteStatusChanges.AsNoTracking().Where(c => c.NoteId == note.Id)
            .OrderBy(c => c.CreatedAt).ToListAsync(ct);
        var names = await NamesAsync(changes.Select(c => c.ChangedById), ct);
        return new NoteHistoryDto(
            signatures.Select(s => new ElectronicSignatureDto(s.Id, s.SignerUserId, s.SignerName, s.Credentials, s.Role, s.Meaning,
                s.SignedAt, s.DisplayTimeZone, s.NoteVersionNumber, LocalTime(s.SignedAt, s.DisplayTimeZone))).ToList(),
            changes.Select(c => new NoteStatusChangeDto(c.Id, c.FromStatus, c.ToStatus, c.ChangedById,
                names.GetValueOrDefault(c.ChangedById), c.Reason, c.CreatedAt)).ToList());
    }

    public async Task<IReadOnlyList<PlanOfCareDto>> ListPlansOfCareAsync(Guid patientId, ICurrentUser actor, CancellationToken ct = default)
    {
        var patient = await _tenantAccess.RequirePatientAccessAsync(actor, patientId, ct: ct);
        var plans = await _db.PlansOfCare.AsNoTracking().Where(p => p.PatientId == patient.Id).ToListAsync(ct);
        return plans.OrderByDescending(p => p.StartDate).ThenByDescending(p => p.CreatedAt).Select(ToPlanOfCareDto).ToList();
    }

    internal static PlanOfCareDto ToPlanOfCareDto(PlanOfCare p) => new(
        p.Id, p.PatientId, p.SourceNoteId, p.PreviousPlanOfCareId, p.Status, p.StartDate, p.EndDate, p.FrequencyPerWeek, p.DurationWeeks,
        p.TreatmentDiagnosis, p.Prognosis, p.RehabPotential, p.PlannedInterventions, p.HomeProgram, p.PatientEducation, p.Referrals,
        p.CertifiedDate, p.CertifyingProviderId, p.DischargeReason, p.DischargeNoteId);

    /// <summary>The original a signing amendment supersedes -- which must
    /// still be Signed (it may have been locked meanwhile).</summary>
    private async Task<ClinicalNote?> LoadAmendedOriginalAsync(ClinicalNote note, CancellationToken ct)
    {
        if (note.AmendsNoteId is not Guid originalId) return null;
        var original = await _db.ClinicalNotes.FirstOrDefaultAsync(n => n.Id == originalId, ct)
            ?? throw new InvalidOperationException("The note being amended no longer exists.");
        if (original.Status != NoteStatus.Signed)
        {
            throw new InvalidOperationException("The original note was locked or already amended, so this amendment can't be signed.");
        }
        return original;
    }

    private async Task AuditAmendmentAsync(ClinicalNote note, ClinicalNote? original, ICurrentUser actor, Organization organization, CancellationToken ct)
    {
        if (original is null) return;
        await _audit.RecordAuditEventAsync(actor.UserId, "note.amended", nameof(ClinicalNote), original.Id, organization.Id,
            patientId: note.PatientId,
            metadata: NoteAudit(original, organization, NoteStatus.Signed, new { amendmentNoteId = note.Id, amendmentReason = note.AmendmentReason }), ct: ct);
    }

    /// <summary>Appends a status-history row (saved with the caller's next SaveChanges).</summary>
    private void RecordStatusChange(ClinicalNote note, NoteStatus? from, NoteStatus to, Guid actorId, string? reason = null) =>
        _db.ClinicalNoteStatusChanges.Add(new ClinicalNoteStatusChange
        {
            NoteId = note.Id,
            FromStatus = from,
            ToStatus = to,
            ChangedById = actorId,
            Reason = reason,
        });

    /// <summary>Connects a new note to its encounter: the appointment's
    /// treating provider (or the author's own provider record) and the
    /// patient's active plan of care.</summary>
    private async Task LinkEncounterAsync(ClinicalNote note, ICurrentUser actor, CancellationToken ct)
    {
        if (note.AppointmentId is Guid appointmentId)
        {
            note.TreatingProviderId = await _db.Appointments.Where(a => a.Id == appointmentId && a.PatientId == note.PatientId)
                .Select(a => a.ProviderId).FirstOrDefaultAsync(ct);
        }
        note.TreatingProviderId ??= await _db.Providers.Where(p => p.UserId == actor.UserId)
            .Select(p => (Guid?)p.Id).FirstOrDefaultAsync(ct);
        var plan = await _db.PlansOfCare.AsNoTracking()
            .Where(p => p.PatientId == note.PatientId && p.Status == PlanOfCareStatus.Active)
            .OrderByDescending(p => p.StartDate).FirstOrDefaultAsync(ct);
        note.PlanOfCareId = plan?.Id;
        // Notes that restate the plan start from the current one (reviewed and edited before signing).
        if (plan is not null && note.NoteType is NoteType.Progress or NoteType.ReEvaluation or NoteType.Recertification)
        {
            note.PlanOfCareStart ??= plan.StartDate;
            note.PlanOfCareEnd ??= plan.EndDate;
            note.FrequencyPerWeek ??= plan.FrequencyPerWeek;
            note.DurationWeeks ??= plan.DurationWeeks;
        }
    }

    /// <summary>One note per visit: a second note for the same appointment is
    /// refused (the existing note can be voided by its author or an
    /// admin/director, which frees the visit). A cancelled or no-show visit
    /// only takes a missed-visit or communication note.</summary>
    private async Task RequireAppointmentFreeAsync(Guid appointmentId, Guid patientId, NoteType type, CancellationToken ct)
    {
        var appointment = await _db.Appointments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == appointmentId && a.PatientId == patientId, ct)
            ?? throw new NotFoundException("The appointment was not found for this patient.");
        var existing = await _db.ClinicalNotes.AsNoTracking().Where(n => n.AppointmentId == appointmentId)
            .Select(n => new { n.NoteType, n.Status }).FirstOrDefaultAsync(ct);
        if (existing is not null)
        {
            throw new InvalidOperationException(
                $"This visit already has a note ({existing.NoteType}, {existing.Status}). Open that note instead; if it shouldn't stand, " +
                "its author or an administrator can void it, which frees the visit for a new note.");
        }
        if (appointment.Status is AppointmentStatus.Cancelled or AppointmentStatus.NoShow && EpisodeRules.IsVisit(type))
            throw new InvalidOperationException("No treatment note is written for a cancelled or no-show visit. Document it with a missed-visit note.");
    }

    private async Task<Guid?> AppointmentTypeIdAsync(Guid? appointmentId, CancellationToken ct) =>
        appointmentId is Guid id
            ? await _db.Appointments.Where(a => a.Id == id).Select(a => a.AppointmentTypeId).FirstOrDefaultAsync(ct)
            : null;

    /// <summary>The supervising PT's user for an assistant's note, when known.</summary>
    private async Task<Guid?> SupervisorUserIdAsync(ClinicalNote note, CancellationToken ct) =>
        note.SupervisingProviderId is Guid supervisor
            ? await _db.Providers.Where(p => p.Id == supervisor).Select(p => p.UserId).FirstOrDefaultAsync(ct)
            : null;

    private async Task<int> LatestVersionNumberAsync(Guid noteId, CancellationToken ct) =>
        await _db.ClinicalNoteVersions.Where(v => v.NoteId == noteId).Select(v => (int?)v.VersionNumber).MaxAsync(ct) ?? 0;

    private async Task<Dictionary<Guid, string>> NamesAsync(IEnumerable<Guid> userIds, CancellationToken ct)
    {
        var ids = userIds.Distinct().ToList();
        var users = await _db.Users.Where(u => ids.Contains(u.Id)).ToListAsync(ct);
        return users.ToDictionary(u => u.Id, u => DisplayName(u, u.Id));
    }

    private async Task<ClinicalNote> LoadNoteInOrgAsync(Guid noteId, ICurrentUser actor, CancellationToken ct)
    {
        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);
        // Addenda are eager-loaded here since every caller of this helper —
        // including GetAsync, which the note-detail page uses to render the
        // addendum list — otherwise gets an always-empty collection.
        var note = await _db.ClinicalNotes.Include(n => n.Addenda).FirstOrDefaultAsync(n => n.Id == noteId, ct)
            ?? throw new NotFoundException("Note was not found.");
        var patient = await _db.Patients.FirstOrDefaultAsync(p => p.Id == note.PatientId, ct);
        if (patient is null || patient.OrganizationId != organization.Id)
        {
            throw new NotFoundException("Note was not found.");
        }
        return note;
    }

    /// <summary>The outcome scores recorded on a note (they are their own
    /// rows, linked by NoteId), for the version snapshot and signature hash.</summary>
    private async Task<IReadOnlyList<OutcomeScore>> NoteOutcomeRowsAsync(Guid noteId, CancellationToken ct) =>
        await _db.OutcomeScores.AsNoTracking().Where(o => o.NoteId == noteId).ToListAsync(ct);

    private static IEnumerable<object> OutcomeContent(IReadOnlyList<OutcomeScore> scores) =>
        scores.OrderBy(o => o.Measure).ThenBy(o => o.MeasuredOn)
            .Select(o => new { o.Measure, o.MeasuredOn, o.Score, o.MaximumScore, o.Interpretation, o.ItemResponsesJson, o.Notes });

    private static string DisplayName(ApplicationUser? user, Guid userId)
    {
        if (user is null) return userId.ToString();
        var name = $"{user.FirstName} {user.LastName}".Trim();
        return string.IsNullOrEmpty(name) ? (user.UserName ?? userId.ToString()) : name;
    }

    /// <summary>SHA-256 over a canonical, field-delimited string of every
    /// clinically-relevant column -- deliberately simple (no external
    /// hashing library, no keyed HMAC) since this is an integrity check
    /// against accidental/out-of-band corruption, not a cryptographic
    /// signature meant to resist a determined attacker with DB access.</summary>
    private static string ComputeContentHash(ClinicalNote note, IReadOnlyList<ClinicalNoteFieldValue> values,
        PainAssessment? pain = null, IReadOnlyList<BodyChartFinding>? findings = null,
        IReadOnlyList<ObjectiveMeasurement>? measurements = null, IReadOnlyList<SpecialTestResult>? specialTests = null,
        IReadOnlyList<NoteIntervention>? interventions = null, IReadOnlyList<NoteGoalProgress>? goalProgress = null,
        IReadOnlyList<OutcomeScore>? outcomes = null)
    {
        var charting = System.Text.Json.JsonSerializer.Serialize(new
        {
            Pain = pain is null ? null : ToPainDto(pain),
            BodyChart = (findings ?? []).Select(f => ToFindingDto(f) with { Id = null }),
            Measurements = (measurements ?? []).OrderBy(m => m.Order).Select(m => ToMeasurementDto(m) with { Id = null }),
            SpecialTests = (specialTests ?? []).OrderBy(r => r.Order).Select(r => ToSpecialTestDto(r) with { Id = null }),
            Interventions = (interventions ?? []).OrderBy(i => i.Order).Select(i => ToFlowsheetDto(i) with { Id = null }),
            GoalProgress = (goalProgress ?? []).OrderBy(p => p.Order).Select(p => ToGoalProgressDto(p) with { Id = null }),
            Outcomes = OutcomeContent(outcomes ?? []),
        });
        var fieldValues = string.Join('\u001E', values.OrderBy(v => v.FieldKey, StringComparer.Ordinal).Select(v =>
            string.Join('\u001D', v.FieldKey, v.ValueText, v.ValueNumber, v.ValueDate, v.ValueTime, v.ValueBool, v.ValueJson)));
        var canonical = string.Join('\u001F',
            note.Id, note.PatientId, note.TherapistId, note.NoteType, note.ServiceDate,
            note.Subjective, note.Objective, note.Interventions, note.Assessment, note.Plan,
            note.DiagnosisSnapshot, note.PrecautionsSnapshot,
            note.SignatureName, note.SignatureCredentials, note.SignedAt, note.SignatureIpAddress,
            note.SubjectiveDetailsJson, note.ObjectiveMeasurementsJson, note.AmendsNoteId, note.AmendmentReason,
            note.TemplateVersionId, fieldValues, charting);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>Saves the note and writes a matching ClinicalNoteVersion
    /// snapshot in the same SaveChanges call -- Id is already assigned
    /// client-side (BaseEntity's default), so the FK is valid even though
    /// neither row has hit the database yet.</summary>
    private async Task<int> SaveWithVersionSnapshotAsync(ClinicalNote note, Guid savedById, bool isSignedVersion, CancellationToken ct,
        ElectronicSignature? signature = null)
    {
        var previousVersionNumber = await _db.ClinicalNoteVersions
            .Where(v => v.NoteId == note.Id).Select(v => (int?)v.VersionNumber).MaxAsync(ct) ?? 0;

        var content = new
        {
            note.NoteType,
            note.ServiceDate,
            note.Subjective,
            note.Objective,
            note.Interventions,
            note.Assessment,
            note.Plan,
            note.PlanOfCareStart,
            note.PlanOfCareEnd,
            note.FrequencyPerWeek,
            note.DurationWeeks,
            note.ReassessmentDue,
            note.Status,
            note.SubjectiveDetailsJson,
            note.ObjectiveMeasurementsJson,
            note.AmendmentReason,
            note.TemplateVersionId,
            FieldValues = (await CurrentFieldValuesAsync(note.Id, ct)).OrderBy(v => v.FieldKey)
                .Select(v => new { v.FieldKey, v.ValueText, v.ValueNumber, v.ValueDate, v.ValueTime, v.ValueBool, v.ValueJson }),
            Pain = (await CurrentPainAsync(note.Id, ct)) is { } pain ? ToPainDto(pain) : null,
            BodyChart = (await CurrentFindingsAsync(note.Id, ct)).Select(ToFindingDto),
            Measurements = (await CurrentRowsAsync(_db.ObjectiveMeasurements, note.Id, ct)).OrderBy(m => m.Order).Select(m => ToMeasurementDto(m) with { Id = null }),
            SpecialTests = (await CurrentRowsAsync(_db.SpecialTestResults, note.Id, ct)).OrderBy(r => r.Order).Select(r => ToSpecialTestDto(r) with { Id = null }),
            Flowsheet = (await CurrentRowsAsync(_db.NoteInterventions, note.Id, ct)).OrderBy(i => i.Order).Select(i => ToFlowsheetDto(i) with { Id = null }),
            GoalProgress = (await CurrentRowsAsync(_db.NoteGoalProgress, note.Id, ct)).OrderBy(p => p.Order).Select(p => ToGoalProgressDto(p) with { Id = null }),
            Outcomes = OutcomeContent(await NoteOutcomeRowsAsync(note.Id, ct)),
        };
        _db.ClinicalNoteVersions.Add(new ClinicalNoteVersion
        {
            NoteId = note.Id,
            VersionNumber = previousVersionNumber + 1,
            ContentJson = JsonSerializer.Serialize(content),
            SavedById = savedById,
            IsSignedVersion = isSignedVersion,
        });
        if (signature is not null)
        {
            // The signature names the exact saved version it signs.
            signature.NoteVersionNumber = previousVersionNumber + 1;
            _db.ElectronicSignatures.Add(signature);
        }

        await _db.SaveChangesAsync(ct);
        return previousVersionNumber + 1;
    }

    /// <summary>A signed note closes the loop on its appointment — never
    /// overrides a cancellation or no-show, since those reflect what
    /// actually happened. Omits the original's `Authorization.visits_used`
    /// increment — that entity isn't ported yet.</summary>
    /// <summary>Password step-up before a signature; a failed attempt is
    /// audited (never the password itself) and rethrown.</summary>
    private async Task VerifySignerAsync(ClinicalNote note, ICurrentUser actor, string? password, string purpose, CancellationToken ct)
    {
        try
        {
            await _signatureVerifier.VerifyAsync(actor.UserId, password, ct);
        }
        catch (SignatureVerificationException)
        {
            var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);
            await _audit.RecordAuditEventAsync(actor.UserId, "note.signature_reauth_failed", nameof(ClinicalNote), note.Id,
                organization.Id, patientId: note.PatientId, metadata: new { purpose }, ct: ct);
            throw;
        }
    }

    private async Task CompleteLinkedAppointmentAsync(ClinicalNote note, CancellationToken ct)
    {
        if (note.AppointmentId is not Guid appointmentId) return;
        var appointment = await _db.Appointments.FirstOrDefaultAsync(a => a.Id == appointmentId, ct);
        if (appointment is null) return;
        if (appointment.Status is not (AppointmentStatus.Scheduled or AppointmentStatus.Confirmed
            or AppointmentStatus.CheckedIn or AppointmentStatus.InProgress)) return;

        appointment.Status = AppointmentStatus.Completed;
        appointment.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}
