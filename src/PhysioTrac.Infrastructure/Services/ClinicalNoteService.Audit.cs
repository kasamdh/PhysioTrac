using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Clinical;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Tenancy;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Infrastructure.Services;

/// <summary>Documentation audit trail: one shape for every note event
/// (note, encounter and appointment ids, previous and new status, display
/// time zone, and the amendment / void / return reason where one applies),
/// plus views, updates, prints and exports. Metadata holds identifiers,
/// statuses and reasons only -- never note content.</summary>
public partial class ClinicalNoteService
{
    private static readonly TimeSpan ViewAuditWindow = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan UpdateAuditWindow = TimeSpan.FromMinutes(15);

    /// <summary>Patient-level printable reports.</summary>
    public static readonly IReadOnlySet<string> PatientReports =
        new HashSet<string> { "plan-of-care", "body-chart", "measurements", "goals", "outcomes" };

    /// <summary>The standard audit metadata for a note event.</summary>
    internal static Dictionary<string, object?> NoteAudit(ClinicalNote note, Organization organization, NoteStatus? previousStatus,
        object? extra = null)
    {
        var metadata = new Dictionary<string, object?>
        {
            ["noteId"] = note.Id,
            // The encounter is the note itself (one note per visit).
            ["encounterId"] = note.Id,
            ["appointmentId"] = note.AppointmentId,
            ["noteType"] = note.NoteType.ToString(),
            ["previousStatus"] = previousStatus?.ToString(),
            ["newStatus"] = note.Status.ToString(),
            ["displayTimeZone"] = organization.Timezone,
        };
        if (note.AmendmentReason is not null) metadata["amendmentReason"] = note.AmendmentReason;
        if (note.Status == NoteStatus.Voided && note.VoidReason is not null) metadata["voidReason"] = note.VoidReason;
        if (note.Status == NoteStatus.ReturnedForCorrection && note.ReturnReason is not null) metadata["returnReason"] = note.ReturnReason;
        if (extra is not null)
            foreach (var p in extra.GetType().GetProperties()) metadata[p.Name] = p.GetValue(extra);
        return metadata;
    }

    private async Task AuditNoteAsync(ClinicalNote note, string action, ICurrentUser actor, NoteStatus? previousStatus,
        object? extra, CancellationToken ct)
    {
        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);
        await _audit.RecordAuditEventAsync(actor.UserId, action, nameof(ClinicalNote), note.Id, organization.Id,
            patientId: note.PatientId, metadata: NoteAudit(note, organization, previousStatus, extra), ct: ct);
    }

    /// <summary>Records an event at most once per person and note within
    /// the window (opening a note again, or autosaving every few seconds,
    /// would otherwise flood the trail; every save is also kept as a version).</summary>
    private async Task AuditNoteOnceAsync(ClinicalNote note, string action, ICurrentUser actor, TimeSpan window, object? extra,
        CancellationToken ct)
    {
        var since = DateTimeOffset.UtcNow - window;
        if (await _db.AuditEvents.AnyAsync(e => e.Action == action && e.ObjectId == note.Id && e.ActorId == actor.UserId &&
            e.CreatedAt >= since, ct)) return;
        await AuditNoteAsync(note, action, actor, null, extra, ct);
    }

    /// <summary>A person opened or read a note.</summary>
    public async Task RecordNoteViewAsync(Guid noteId, ICurrentUser actor, CancellationToken ct = default)
    {
        var note = await GetAsync(noteId, actor, ct);
        await AuditNoteOnceAsync(note, "note.viewed", actor, ViewAuditWindow, null, ct);
    }

    /// <summary>Prints or exports (PDF) a note: allowed to whoever may view
    /// it, and recorded. <paramref name="kind"/> is "print" or "export".</summary>
    public async Task RecordNoteOutputAsync(Guid noteId, string kind, ICurrentUser actor, CancellationToken ct = default)
    {
        var action = OutputAction("note", kind);
        var note = await GetAsync(noteId, actor, ct);
        await AuditNoteAsync(note, action, actor, null, null, ct);
    }

    /// <summary>Prints or exports a patient report (plan of care, body
    /// chart, measurement comparison, goal progress, outcome history).</summary>
    public async Task RecordPatientReportOutputAsync(Guid patientId, string report, string kind, ICurrentUser actor, CancellationToken ct = default)
    {
        var action = OutputAction("patient_report", kind);
        if (!PatientReports.Contains(report)) throw new InvalidOperationException("Unknown report.");
        _tenantAccess.RequireRole(actor, RoleSets.Clinical);
        var patient = await _tenantAccess.RequirePatientAccessAsync(actor, patientId, ct: ct);
        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);
        await _audit.RecordAuditEventAsync(actor.UserId, action, nameof(Patient), patient.Id, organization.Id, patientId: patient.Id,
            metadata: new { report, displayTimeZone = organization.Timezone }, ct: ct);
    }

    private static string OutputAction(string prefix, string kind) => kind switch
    {
        "print" => $"{prefix}.printed",
        "export" => $"{prefix}.exported",
        _ => throw new InvalidOperationException("Choose print or export."),
    };

    /// <summary>Body charts from the patient's signed notes, newest first.</summary>
    public async Task<IReadOnlyList<BodyChartHistoryDto>> GetBodyChartHistoryAsync(Guid patientId, ICurrentUser actor, CancellationToken ct = default)
    {
        var patient = await _tenantAccess.RequirePatientAccessAsync(actor, patientId, ct: ct);
        var notes = await _db.ClinicalNotes.AsNoTracking()
            .Where(n => n.PatientId == patient.Id && (n.Status == NoteStatus.Signed || n.Status == NoteStatus.Locked) &&
                _db.BodyChartFindings.Any(f => f.NoteId == n.Id))
            .OrderByDescending(n => n.ServiceDate).ThenByDescending(n => n.CreatedAt)
            .Select(n => new { n.Id, n.ServiceDate, n.NoteType }).Take(10).ToListAsync(ct);
        var ids = notes.Select(n => n.Id).ToList();
        var findings = await _db.BodyChartFindings.AsNoTracking().Where(f => ids.Contains(f.NoteId)).OrderBy(f => f.Order).ToListAsync(ct);
        return notes.Select(n => new BodyChartHistoryDto(n.Id, n.ServiceDate, n.NoteType,
            findings.Where(f => f.NoteId == n.Id).Select(ToFindingDto).ToList())).ToList();
    }
}
