using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Audit;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Clinical;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Tenancy;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Identity;
using PhysioTrac.Infrastructure.Persistence;

namespace PhysioTrac.Infrastructure.Services;

/// <summary>Direct port of `care/note_management.py`.</summary>
public class ClinicalNoteService : IClinicalNoteService
{
    private static readonly HashSet<UserRole> FinalizingRoles = new() { UserRole.Admin, UserRole.Director };

    private readonly PhysioTracDbContext _db;
    private readonly ITenantAccessService _tenantAccess;
    private readonly IAuditService _audit;

    public ClinicalNoteService(PhysioTracDbContext db, ITenantAccessService tenantAccess, IAuditService audit)
    {
        _db = db;
        _tenantAccess = tenantAccess;
        _audit = audit;
    }

    public bool CanViewNote(ICurrentUser user, ClinicalNote note) =>
        FinalizingRoles.Contains(user.Role) || note.TherapistId == user.UserId;

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

    private static bool CanSignNotes(UserRole role) => role is UserRole.Admin or UserRole.Director or UserRole.Therapist;

    public async Task<ClinicalNote> CreateDraftAsync(CreateNoteRequest request, ICurrentUser actor, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, RoleSets.Clinical);
        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);

        var patient = await _db.Patients.FirstOrDefaultAsync(p => p.Id == request.PatientId && p.OrganizationId == organization.Id, ct)
            ?? throw new NotFoundException("Patient was not found.");

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
            PlanOfCareStart = request.PlanOfCareStart,
            PlanOfCareEnd = request.PlanOfCareEnd,
            FrequencyPerWeek = request.FrequencyPerWeek,
            DurationWeeks = request.DurationWeeks,
            ReassessmentDue = request.ReassessmentDue,
            // Snapshotted at creation so a later org-policy change never
            // silently changes an in-progress note's cosign requirement.
            CosignRequired = organization.PtaCosignRequired && actor.Role == UserRole.Assistant,
        };
        _db.ClinicalNotes.Add(note);
        await SaveWithVersionSnapshotAsync(note, actor.UserId, isSignedVersion: false, ct);

        await _audit.RecordAuditEventAsync(actor.UserId, "note.created", nameof(ClinicalNote), note.Id, organization.Id,
            patientId: patient.Id, metadata: new { noteType = note.NoteType.ToString() }, ct: ct);

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
        await SaveWithVersionSnapshotAsync(note, actor.UserId, isSignedVersion: false, ct);
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

    public async Task<ClinicalNote> SignNoteAsync(Guid noteId, bool attestationConfirmed, string? ipAddress, ICurrentUser actor, CancellationToken ct = default)
    {
        var note = await LoadNoteInOrgAsync(noteId, actor, ct);
        if (!CanFinalizeNote(actor, note))
        {
            throw new ForbiddenException("You are not permitted to finalize this note.");
        }
        if (!attestationConfirmed)
        {
            throw new InvalidOperationException("Confirm therapist review and attestation before finalizing this note.");
        }

        var blockers = NoteComplianceEvaluator.Evaluate(note).Where(f => f.FinalizationBlocker).ToList();
        if (blockers.Count > 0)
        {
            throw new InvalidOperationException(
                "Note cannot be finalized until required documentation checks are resolved: " +
                string.Join(", ", blockers.Select(b => b.Code)));
        }

        var signer = await _db.Users.FirstOrDefaultAsync(u => u.Id == actor.UserId, ct);
        note.SignatureName = DisplayName(signer, actor.UserId);
        note.SignatureCredentials = signer?.Credential;
        note.SignedAt = DateTimeOffset.UtcNow;
        note.SignatureIpAddress = ipAddress;
        note.FinalizationAttestation = true;
        var pendingCosign = note.CosignRequired && actor.Role == UserRole.Assistant;
        note.Status = pendingCosign ? NoteStatus.ReviewRequired : NoteStatus.Signed;
        note.UpdatedAt = DateTimeOffset.UtcNow;
        // Computed after every other field above is set, so the hash covers
        // the exact content -- including the signature metadata itself --
        // that becomes immutable from this point on.
        note.SignatureHash = ComputeContentHash(note);

        await SaveWithVersionSnapshotAsync(note, actor.UserId, isSignedVersion: true, ct);

        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);
        await _audit.RecordAuditEventAsync(actor.UserId, pendingCosign ? "note.submitted_for_cosign" : "note.signed",
            nameof(ClinicalNote), note.Id, organization.Id, patientId: note.PatientId,
            metadata: new { noteType = note.NoteType.ToString(), status = note.Status.ToString() }, ct: ct);

        if (note.Status == NoteStatus.Signed)
        {
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

        note.Status = NoteStatus.Locked;
        note.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);
        await _audit.RecordAuditEventAsync(actor.UserId, "note.locked", nameof(ClinicalNote), note.Id, organization.Id,
            patientId: note.PatientId, ct: ct);

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

    public async Task<ClinicalNote> CosignNoteAsync(Guid noteId, ICurrentUser actor, CancellationToken ct = default)
    {
        var note = await LoadNoteInOrgAsync(noteId, actor, ct);
        if (!CanCosignNote(actor, note))
        {
            throw new ForbiddenException("You are not permitted to cosign this note.");
        }
        if (note.Status != NoteStatus.ReviewRequired)
        {
            throw new InvalidOperationException("Only a note awaiting cosign can be cosigned.");
        }

        note.CosignedById = actor.UserId;
        note.CosignedAt = DateTimeOffset.UtcNow;
        note.Status = NoteStatus.Signed;
        note.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);
        await _audit.RecordAuditEventAsync(actor.UserId, "note.cosigned", nameof(ClinicalNote), note.Id, organization.Id,
            patientId: note.PatientId, metadata: new { noteType = note.NoteType.ToString() }, ct: ct);

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

        var addendum = new NoteAddendum { NoteId = note.Id, AuthorId = actor.UserId, Reason = request.Reason, Body = request.Body };
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

        var intervention = new NoteIntervention
        {
            NoteId = note.Id, Description = request.Description, BodyRegion = request.BodyRegion,
            Category = request.Category, Minutes = request.Minutes, Units = request.Units,
            IsTimed = request.IsTimed, Order = request.Order,
        };
        _db.NoteInterventions.Add(intervention);
        await _db.SaveChangesAsync(ct);
        return intervention;
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
    private static string ComputeContentHash(ClinicalNote note)
    {
        var canonical = string.Join('\u001F',
            note.Id, note.PatientId, note.TherapistId, note.NoteType, note.ServiceDate,
            note.Subjective, note.Objective, note.Interventions, note.Assessment, note.Plan,
            note.DiagnosisSnapshot, note.PrecautionsSnapshot,
            note.SignatureName, note.SignatureCredentials, note.SignedAt, note.SignatureIpAddress);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>Saves the note and writes a matching ClinicalNoteVersion
    /// snapshot in the same SaveChanges call -- Id is already assigned
    /// client-side (BaseEntity's default), so the FK is valid even though
    /// neither row has hit the database yet.</summary>
    private async Task SaveWithVersionSnapshotAsync(ClinicalNote note, Guid savedById, bool isSignedVersion, CancellationToken ct)
    {
        var previousVersionNumber = await _db.ClinicalNoteVersions
            .Where(v => v.NoteId == note.Id).Select(v => (int?)v.VersionNumber).MaxAsync(ct) ?? 0;

        var content = new
        {
            note.NoteType, note.ServiceDate, note.Subjective, note.Objective, note.Interventions,
            note.Assessment, note.Plan, note.PlanOfCareStart, note.PlanOfCareEnd,
            note.FrequencyPerWeek, note.DurationWeeks, note.ReassessmentDue, note.Status,
        };
        _db.ClinicalNoteVersions.Add(new ClinicalNoteVersion
        {
            NoteId = note.Id,
            VersionNumber = previousVersionNumber + 1,
            ContentJson = JsonSerializer.Serialize(content),
            SavedById = savedById,
            IsSignedVersion = isSignedVersion,
        });

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>A signed note closes the loop on its appointment — never
    /// overrides a cancellation or no-show, since those reflect what
    /// actually happened. Omits the original's `Authorization.visits_used`
    /// increment — that entity isn't ported yet.</summary>
    private async Task CompleteLinkedAppointmentAsync(ClinicalNote note, CancellationToken ct)
    {
        if (note.AppointmentId is not Guid appointmentId) return;
        var appointment = await _db.Appointments.FirstOrDefaultAsync(a => a.Id == appointmentId, ct);
        if (appointment is null) return;
        if (appointment.Status is not (AppointmentStatus.Scheduled or AppointmentStatus.CheckedIn)) return;

        appointment.Status = AppointmentStatus.Completed;
        appointment.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}
