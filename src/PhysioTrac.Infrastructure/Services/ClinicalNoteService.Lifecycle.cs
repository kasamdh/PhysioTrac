using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Clinical;
using PhysioTrac.Application.Common;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Infrastructure.Services;

/// <summary>The review part of the documentation lifecycle: a supervising PT
/// starts reviewing an assistant's submitted note, then cosigns it
/// (CosignNoteAsync) or returns it for correction; and voiding a note with a
/// reason. Every step records a status change, a version and an audit event.</summary>
public partial class ClinicalNoteService
{
    /// <summary>Draft / returned notes: the author or an admin/director.
    /// Signed or locked notes: an admin/director, or the PT who wrote it.
    /// Never a note under review (return it first), an amended original
    /// (void its amendment), a signed amendment, or one already voided.</summary>
    public bool CanVoidNote(ICurrentUser user, ClinicalNote note)
    {
        if (note.Status is NoteStatus.Voided or NoteStatus.Amended || LifecycleRules.IsAwaitingReview(note.Status)) return false;
        if (note.IsEditable) return FinalizingRoles.Contains(user.Role) || note.TherapistId == user.UserId;
        if (note.AmendsNoteId is not null) return false;
        return FinalizingRoles.Contains(user.Role) || (note.TherapistId == user.UserId && CanSignNotes(user.Role));
    }

    public async Task<ClinicalNote> StartReviewAsync(Guid noteId, ICurrentUser actor, CancellationToken ct = default)
    {
        var note = await LoadNoteInOrgAsync(noteId, actor, ct);
        if (!CanCosignNote(actor, note)) throw new ForbiddenException("You are not permitted to review this note.");
        if (note.Status != NoteStatus.ReviewRequired) throw new InvalidOperationException("Only a submitted note waiting for review can be reviewed.");

        RecordStatusChange(note, note.Status, NoteStatus.InReview, actor.UserId);
        note.Status = NoteStatus.InReview;
        note.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);
        await _audit.RecordAuditEventAsync(actor.UserId, "note.review_started", nameof(ClinicalNote), note.Id, organization.Id,
            patientId: note.PatientId, ct: ct);
        return note;
    }

    /// <summary>The supervising PT sends a submitted note back to its author
    /// with a reason. The author's signature is withdrawn from the note (it
    /// stays in the signature history) and the note can be edited again.</summary>
    public async Task<ClinicalNote> ReturnForCorrectionAsync(Guid noteId, string reason, ICurrentUser actor, CancellationToken ct = default)
    {
        var note = await LoadNoteInOrgAsync(noteId, actor, ct);
        if (!CanCosignNote(actor, note)) throw new ForbiddenException("You are not permitted to return this note.");
        if (!LifecycleRules.IsAwaitingReview(note.Status)) throw new InvalidOperationException("Only a note awaiting review can be returned.");
        var why = RequireReason(reason, "Say what needs to be corrected.");

        RecordStatusChange(note, note.Status, NoteStatus.ReturnedForCorrection, actor.UserId, why);
        note.Status = NoteStatus.ReturnedForCorrection;
        note.ReturnReason = why;
        note.SignatureName = null;
        note.SignatureCredentials = null;
        note.SignedAt = null;
        note.SignatureIpAddress = null;
        note.SignatureHash = null;
        note.FinalizationAttestation = false;
        note.UpdatedAt = DateTimeOffset.UtcNow;
        foreach (var request in await _db.NoteCosignRequests.Where(r => r.NoteId == note.Id && r.Status == CosignRequestStatus.Pending).ToListAsync(ct))
        {
            request.Status = CosignRequestStatus.Returned;
            request.ResolvedAt = DateTimeOffset.UtcNow;
            request.ResolvedById = actor.UserId;
        }
        await SaveWithVersionSnapshotAsync(note, actor.UserId, isSignedVersion: false, ct);

        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);
        await _audit.RecordAuditEventAsync(actor.UserId, "note.returned_for_correction", nameof(ClinicalNote), note.Id, organization.Id,
            patientId: note.PatientId, ct: ct);
        return note;
    }

    /// <summary>Withdraws a note with a reason. It is kept (never deleted)
    /// with all its versions and signatures. Voiding a signed note
    /// re-confirms the voider's password and adds a "void" signature; a plan
    /// of care the note created is voided too, and the plan it had replaced
    /// becomes active again.</summary>
    public async Task<ClinicalNote> VoidNoteAsync(Guid noteId, VoidNoteRequest request, ICurrentUser actor, CancellationToken ct = default)
    {
        var note = await LoadNoteInOrgAsync(noteId, actor, ct);
        if (!CanVoidNote(actor, note))
        {
            if (note.Status == NoteStatus.Voided) throw new InvalidOperationException("This note is already voided.");
            if (LifecycleRules.IsAwaitingReview(note.Status)) throw new InvalidOperationException("Return the note for correction before voiding it.");
            throw new ForbiddenException("You are not permitted to void this note.");
        }
        var why = RequireReason(request.Reason, "Give the reason for voiding the note.");
        var wasSigned = note.IsSigned;
        if (wasSigned) await VerifySignerAsync(note, actor, request.Password, "void", ct);

        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);
        var appointmentId = note.AppointmentId;
        RecordStatusChange(note, note.Status, NoteStatus.Voided, actor.UserId, why);
        note.Status = NoteStatus.Voided;
        note.VoidReason = why;
        note.VoidedAt = DateTimeOffset.UtcNow;
        note.VoidedById = actor.UserId;
        // Frees the visit for a new note; the link stays in the audit event.
        note.AppointmentId = null;
        note.UpdatedAt = DateTimeOffset.UtcNow;

        var plans = await _db.PlansOfCare.Where(p => p.SourceNoteId == note.Id &&
            (p.Status == PlanOfCareStatus.Active || p.Status == PlanOfCareStatus.Draft)).ToListAsync(ct);
        foreach (var plan in plans)
        {
            plan.Status = PlanOfCareStatus.Voided;
            if (plan.PreviousPlanOfCareId is Guid previousId &&
                await _db.PlansOfCare.FirstOrDefaultAsync(p => p.Id == previousId && p.Status == PlanOfCareStatus.Superseded, ct) is { } previous)
            {
                previous.Status = PlanOfCareStatus.Active;
            }
        }

        ElectronicSignature? signature = null;
        if (wasSigned)
        {
            var voider = await _db.Users.FirstOrDefaultAsync(u => u.Id == actor.UserId, ct);
            signature = new ElectronicSignature
            {
                NoteId = note.Id,
                SignerUserId = actor.UserId,
                SignerName = DisplayName(voider, actor.UserId),
                Credentials = voider?.Credential,
                Role = actor.Role.ToString(),
                Meaning = SignatureMeaning.Void,
                SignedAt = note.VoidedAt.Value,
                DisplayTimeZone = organization.Timezone,
                ContentHash = note.SignatureHash,
            };
        }
        await SaveWithVersionSnapshotAsync(note, actor.UserId, isSignedVersion: false, ct, signature);

        await _audit.RecordAuditEventAsync(actor.UserId, "note.voided", nameof(ClinicalNote), note.Id, organization.Id,
            patientId: note.PatientId,
            metadata: new { wasSigned, appointmentId, plansVoided = plans.Count }, ct: ct);
        return note;
    }

    /// <summary>Refuses an action taken on an out-of-date copy of the note
    /// (someone saved after the caller loaded it). Null skips the check.</summary>
    private async Task RequireSaveVersionAsync(ClinicalNote note, int? expected, CancellationToken ct)
    {
        if (expected is not int version) return;
        var latest = await _db.ClinicalNoteVersions.Where(v => v.NoteId == note.Id).OrderByDescending(v => v.VersionNumber)
            .Select(v => new { v.VersionNumber, v.CreatedAt, v.SavedById }).FirstOrDefaultAsync(ct);
        if ((latest?.VersionNumber ?? 0) == version) return;
        var who = latest is null ? null : (await NamesAsync([latest.SavedById], ct)).GetValueOrDefault(latest.SavedById);
        throw new EncounterConflictException(latest?.VersionNumber ?? 0, latest?.CreatedAt ?? note.UpdatedAt, who);
    }

    private static string RequireReason(string? reason, string message)
    {
        var why = reason?.Trim();
        if (string.IsNullOrEmpty(why)) throw new InvalidOperationException(message);
        if (why.Length > LifecycleRules.MaxReasonLength)
            throw new InvalidOperationException($"The reason is longer than {LifecycleRules.MaxReasonLength} characters.");
        return why;
    }

    /// <summary>A signature time in the organization's display time zone,
    /// e.g. "10/08/2026 2:30 PM (America/New_York)".</summary>
    internal static string? LocalTime(DateTimeOffset utc, string timeZone)
    {
        try
        {
            var local = TimeZoneInfo.ConvertTime(utc, TimeZoneInfo.FindSystemTimeZoneById(timeZone));
            return $"{local.ToString("MM/dd/yyyy h:mm tt", CultureInfo.InvariantCulture)} ({timeZone})";
        }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return null;
        }
    }
}
