using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Clinical;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Tenancy;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Infrastructure.Services;

/// <summary>The documentation dashboard: today's visits and their notes,
/// visits not documented yet, unsigned notes by state, overdue notes,
/// progress-note / re-evaluation / plan-of-care deadlines and recently signed
/// notes -- for the patients the caller may see, with optional filters.</summary>
public partial class ClinicalNoteService
{
    private const int DashboardListLimit = 200;

    public async Task<DocumentationDashboardDto> GetDashboardAsync(DashboardFilter filter, ICurrentUser actor, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, RoleSets.Clinical);
        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);
        var zone = Tz(organization.Timezone);
        var now = DateTimeOffset.UtcNow;
        var today = LocalDate(now, zone);
        var to = filter.To ?? today;
        if (filter.From is DateOnly f && f > to) throw new InvalidOperationException("The start date is after the end date.");

        var patients = _tenantAccess.PatientsFor(actor, clinical: false);
        if (filter.PatientId is Guid onlyPatient) patients = patients.Where(p => p.Id == onlyPatient);

        Guid? providerId = filter.ProviderId;
        Guid? providerUserId = null;
        if (providerId is Guid pid)
        {
            var provider = await _db.Providers.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pid && p.OrganizationId == organization.Id, ct)
                ?? throw new NotFoundException("Provider was not found.");
            providerUserId = provider.UserId;
        }

        bool TypeMatches(NoteType? type) => filter.NoteType is null || type == filter.NoteType;
        bool StatusMatches(DocumentationStatus? status) => filter.Status is null || status == filter.Status;

        // ---- visits: today's schedule and visits with no note yet
        var visitsFrom = filter.From ?? today.AddDays(-30);
        var rangeStart = Utc(Min(visitsFrom, today), zone);
        var rangeEnd = Utc((to > today ? to : today).AddDays(1), zone);
        var visitRows = await (
            from a in _db.Appointments.AsNoTracking()
            join p in patients on a.PatientId equals p.Id
            where a.StartsAt >= rangeStart && a.StartsAt < rangeEnd &&
                (providerId == null || a.ProviderId == providerId || (a.ProviderId == null && a.TherapistId == providerUserId))
            orderby a.StartsAt
            select new
            {
                a.Id,
                a.PatientId,
                p.FirstName,
                p.LastName,
                p.MedicalRecordNumber,
                a.StartsAt,
                a.Status,
                a.Kind,
                DefaultNoteType = a.AppointmentType != null ? a.AppointmentType.DefaultNoteType : null,
                a.ProviderId,
            }).Take(1000).ToListAsync(ct);
        var appointmentIds = visitRows.Select(v => v.Id).ToList();
        var visitNotes = (await _db.ClinicalNotes.AsNoTracking()
                .Where(n => n.AppointmentId != null && appointmentIds.Contains(n.AppointmentId.Value))
                .Select(n => new { AppointmentId = n.AppointmentId!.Value, n.Id, n.NoteType, n.Status, n.CosignedAt, n.TherapistId })
                .ToListAsync(ct))
            .GroupBy(n => n.AppointmentId).ToDictionary(g => g.Key, g => g.First());

        // ---- unsigned notes
        var unsignedNotes = await (
            from n in _db.ClinicalNotes
            join p in patients on n.PatientId equals p.Id
            where (n.Status == NoteStatus.Draft || n.Status == NoteStatus.ReturnedForCorrection ||
                   n.Status == NoteStatus.ReviewRequired || n.Status == NoteStatus.InReview) &&
                n.ServiceDate <= to && (filter.From == null || n.ServiceDate >= filter.From) &&
                (filter.NoteType == null || n.NoteType == filter.NoteType) &&
                (providerId == null || n.TreatingProviderId == providerId || (n.TreatingProviderId == null && n.TherapistId == providerUserId))
            orderby n.ServiceDate
            select n).Take(DashboardListLimit).ToListAsync(ct);

        // ---- recently signed
        var signedSince = filter.From is DateOnly from ? Utc(from, zone) : now.AddDays(-7);
        var recentlySigned = await (
            from n in _db.ClinicalNotes.AsNoTracking()
            join p in patients on n.PatientId equals p.Id
            where (n.Status == NoteStatus.Signed || n.Status == NoteStatus.Locked) &&
                (n.CosignedAt ?? n.SignedAt) >= signedSince && n.ServiceDate <= to &&
                (filter.NoteType == null || n.NoteType == filter.NoteType) &&
                (providerId == null || n.TreatingProviderId == providerId || (n.TreatingProviderId == null && n.TherapistId == providerUserId))
            orderby n.CosignedAt ?? n.SignedAt descending
            select new { n.Id, n.PatientId, n.NoteType, n.Status, n.ServiceDate, n.TherapistId, n.TreatingProviderId, n.SignedAt, n.CosignedAt })
            .Take(50).ToListAsync(ct);

        // ---- names
        var patientIds = visitRows.Select(v => v.PatientId).Concat(unsignedNotes.Select(n => n.PatientId))
            .Concat(recentlySigned.Select(n => n.PatientId)).Distinct().ToList();
        var patientInfo = await _db.Patients.AsNoTracking().Where(p => patientIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => (Name: (p.FirstName + " " + p.LastName).Trim(), p.MedicalRecordNumber), ct);
        var providerIds = visitRows.Select(v => v.ProviderId).Concat(unsignedNotes.Select(n => n.TreatingProviderId))
            .Concat(recentlySigned.Select(n => n.TreatingProviderId)).OfType<Guid>().Distinct().ToList();
        var providerNames = (await _db.Providers.AsNoTracking().Where(p => providerIds.Contains(p.Id)).ToListAsync(ct))
            .ToDictionary(p => p.Id, p => p.Credentials is null ? p.FullName : $"{p.FullName}, {p.Credentials}");
        var authors = await NamesAsync(unsignedNotes.Select(n => n.TherapistId).Concat(recentlySigned.Select(n => n.TherapistId))
            .Concat(visitNotes.Values.Select(n => n.TherapistId)), ct);
        string? ProviderName(Guid? id) => id is Guid g && providerNames.TryGetValue(g, out var name) ? name : null;

        var visits = visitRows.Select(v =>
        {
            var date = LocalDate(v.StartsAt, zone);
            var note = visitNotes.GetValueOrDefault(v.Id);
            var missed = v.Status is AppointmentStatus.Cancelled or AppointmentStatus.NoShow;
            var status = note is not null ? LifecycleRules.For(note.Status, note.CosignedAt is not null)
                : missed ? (DocumentationStatus?)null : DocumentationStatus.NotStarted;
            var type = note?.NoteType ?? (missed ? NoteType.MissedVisit : EncounterRules.NoteTypeFor(v.Kind, v.DefaultNoteType));
            var undocumented = note is null && !missed && v.StartsAt <= now;
            return new DashboardItemDto(v.PatientId, $"{v.FirstName} {v.LastName}".Trim(), v.MedicalRecordNumber, date,
                note?.Id, type, note?.Status, status, v.Id, v.StartsAt, v.Status,
                note is null ? null : authors.GetValueOrDefault(note.TherapistId), ProviderName(v.ProviderId),
                today.DayNumber - date.DayNumber, undocumented && DashboardRules.IsOverdue(date, today),
                Detail: missed && note is null ? "Cancelled or no-show visit" : null);
        }).ToList();

        var todays = visits.Where(v => v.Date == today && TypeMatches(v.NoteType) && StatusMatches(v.DocumentationStatus)).ToList();
        var notStarted = visits.Where(v => v.DocumentationStatus == DocumentationStatus.NotStarted && v.StartsAt <= now &&
            v.Date >= visitsFrom && v.Date <= to && TypeMatches(v.NoteType) && StatusMatches(v.DocumentationStatus))
            .OrderByDescending(v => v.StartsAt).ToList(); // most recent first

        var drafts = new List<DashboardItemDto>();
        var ready = new List<DashboardItemDto>();
        var cosign = new List<DashboardItemDto>();
        var returned = new List<DashboardItemDto>();
        foreach (var n in unsignedNotes)
        {
            var status = n.Status switch
            {
                NoteStatus.ReturnedForCorrection => DocumentationStatus.ReturnedForCorrection,
                NoteStatus.Draft => (await ComplianceAsync(n, ct)).Any(c => c.FinalizationBlocker) ? DocumentationStatus.Draft : DocumentationStatus.ReadyToSign,
                _ => LifecycleRules.For(n.Status, false),
            };
            if (!StatusMatches(status)) continue;
            var info = patientInfo.GetValueOrDefault(n.PatientId);
            var item = new DashboardItemDto(n.PatientId, info.Name ?? "", info.MedicalRecordNumber ?? "", n.ServiceDate,
                n.Id, n.NoteType, n.Status, status, n.AppointmentId, null, null,
                authors.GetValueOrDefault(n.TherapistId), ProviderName(n.TreatingProviderId),
                today.DayNumber - n.ServiceDate.DayNumber, DashboardRules.IsOverdue(n.ServiceDate, today),
                Detail: n.Status == NoteStatus.ReturnedForCorrection ? n.ReturnReason : null);
            (status switch
            {
                DocumentationStatus.ReadyToSign => ready,
                DocumentationStatus.ReturnedForCorrection => returned,
                DocumentationStatus.CosignRequired or DocumentationStatus.InReview => cosign,
                _ => drafts,
            }).Add(item);
        }
        var overdue = drafts.Concat(ready).Concat(cosign).Concat(returned).Where(i => i.Overdue)
            .Concat(notStarted.Where(v => v.Overdue)).OrderBy(i => i.Date).ToList();

        var signed = recentlySigned.Select(n =>
        {
            var info = patientInfo.GetValueOrDefault(n.PatientId);
            var status = LifecycleRules.For(n.Status, n.CosignedAt is not null);
            return new DashboardItemDto(n.PatientId, info.Name ?? "", info.MedicalRecordNumber ?? "", n.ServiceDate,
                n.Id, n.NoteType, n.Status, status, null, null, null,
                authors.GetValueOrDefault(n.TherapistId), ProviderName(n.TreatingProviderId),
                today.DayNumber - n.ServiceDate.DayNumber, false, n.CosignedAt ?? n.SignedAt);
        }).Where(i => StatusMatches(i.DocumentationStatus)).ToList();

        var (progress, reevaluations, expiring) = await DeadlinesAsync(patients, organization.ProgressNoteDueVisitCount, providerId, providerUserId, today, ct);
        progress = progress.Where(d => TypeMatches(d.NoteType)).ToList();
        reevaluations = reevaluations.Where(d => TypeMatches(d.NoteType)).ToList();
        expiring = expiring.Where(d => TypeMatches(d.NoteType)).ToList();

        return new DocumentationDashboardDto(today, filter.From, to,
            new DashboardCountsDto(todays.Count, notStarted.Count, drafts.Count, ready.Count, cosign.Count, returned.Count, overdue.Count,
                progress.Count, reevaluations.Count, expiring.Count, signed.Count),
            todays, notStarted, drafts, ready, cosign, returned, overdue, progress, reevaluations, expiring, signed);
    }

    /// <summary>Deadlines for patients with an active plan of care (and, with
    /// a provider filter, seen by that provider in the last 90 days).</summary>
    private async Task<(IReadOnlyList<DashboardDeadlineDto> Progress, IReadOnlyList<DashboardDeadlineDto> Reevaluations,
        IReadOnlyList<DashboardDeadlineDto> Expiring)> DeadlinesAsync(IQueryable<Domain.Entities.Patient> patients, int? visitCount,
        Guid? providerId, Guid? providerUserId, DateOnly today, CancellationToken ct)
    {
        var plans = await (
            from pl in _db.PlansOfCare.AsNoTracking()
            join p in patients on pl.PatientId equals p.Id
            where pl.Status == PlanOfCareStatus.Active
            select new { Plan = pl, p.FirstName, p.LastName, p.MedicalRecordNumber }).Take(1000).ToListAsync(ct);
        if (providerId is not null)
        {
            var since = today.AddDays(-90);
            var sinceAt = DateTimeOffset.UtcNow.AddDays(-90);
            var seen = (await _db.ClinicalNotes.AsNoTracking()
                    .Where(n => n.ServiceDate >= since && (n.TreatingProviderId == providerId || n.TherapistId == providerUserId))
                    .Select(n => n.PatientId).Distinct().ToListAsync(ct))
                .Concat(await _db.Appointments.AsNoTracking()
                    .Where(a => a.StartsAt >= sinceAt && (a.ProviderId == providerId || a.TherapistId == providerUserId))
                    .Select(a => a.PatientId).Distinct().ToListAsync(ct))
                .ToHashSet();
            plans = plans.Where(p => seen.Contains(p.Plan.PatientId)).ToList();
        }
        var ids = plans.Select(p => p.Plan.PatientId).Distinct().ToList();
        var notes = await _db.ClinicalNotes.AsNoTracking()
            .Where(n => ids.Contains(n.PatientId) && (n.Status == NoteStatus.Signed || n.Status == NoteStatus.Locked))
            .Select(n => new { n.PatientId, n.NoteType, n.ServiceDate, n.CreatedAt, n.ReassessmentDue }).ToListAsync(ct);

        var threshold = visitCount ?? DashboardRules.DefaultProgressVisitCount;
        var progress = new List<DashboardDeadlineDto>();
        var reevaluations = new List<DashboardDeadlineDto>();
        var expiring = new List<DashboardDeadlineDto>();
        foreach (var row in plans.GroupBy(p => p.Plan.PatientId).Select(g => g.OrderByDescending(p => p.Plan.StartDate).First()))
        {
            var name = $"{row.FirstName} {row.LastName}".Trim();
            var mine = notes.Where(n => n.PatientId == row.Plan.PatientId).ToList();
            var trigger = mine.Where(n => n.NoteType is NoteType.Evaluation or NoteType.PelvicHealthEvaluation or NoteType.Progress
                    or NoteType.ReEvaluation or NoteType.Recertification)
                .OrderByDescending(n => n.ServiceDate).ThenByDescending(n => n.CreatedAt).FirstOrDefault();
            var visitsSince = mine.Count(n => n.NoteType is NoteType.Daily or NoteType.Soap or NoteType.HomeVisit or NoteType.DryNeedlingTreatment &&
                (trigger is null || n.ServiceDate > trigger.ServiceDate || (n.ServiceDate == trigger.ServiceDate && n.CreatedAt > trigger.CreatedAt)));
            if (visitsSince >= threshold - DashboardRules.ProgressDueSoonVisits)
            {
                progress.Add(new DashboardDeadlineDto(row.Plan.PatientId, name, row.MedicalRecordNumber, NoteType.Progress, null,
                    visitsSince >= threshold,
                    $"{visitsSince} visit{(visitsSince == 1 ? "" : "s")} since the last {(trigger is null ? "evaluation" : "evaluation or progress note")}" +
                    $"{(trigger is null ? "" : " of " + trigger.ServiceDate.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture))} — due at visit {threshold}",
                    row.Plan.Id));
            }
            if (trigger?.ReassessmentDue is DateOnly due && due <= today.AddDays(DashboardRules.ReevaluationWindowDays))
            {
                reevaluations.Add(new DashboardDeadlineDto(row.Plan.PatientId, name, row.MedicalRecordNumber, NoteType.ReEvaluation, due,
                    due < today, due < today ? $"Reassessment was due {Days(today.DayNumber - due.DayNumber)} ago" : $"Reassessment due in {Days(due.DayNumber - today.DayNumber)}",
                    row.Plan.Id));
            }
            if (row.Plan.EndDate <= today.AddDays(DashboardRules.ExpiringPlanWindowDays))
            {
                var left = row.Plan.EndDate.DayNumber - today.DayNumber;
                expiring.Add(new DashboardDeadlineDto(row.Plan.PatientId, name, row.MedicalRecordNumber, NoteType.Recertification, row.Plan.EndDate,
                    left < 0, left < 0 ? $"Certification ended {Days(-left)} ago — recertify or discharge" : $"Certification ends in {Days(left)}",
                    row.Plan.Id));
            }
        }
        return (progress.OrderByDescending(p => p.Overdue).ThenBy(p => p.PatientName).ToList(),
            reevaluations.OrderBy(r => r.DueDate).ToList(), expiring.OrderBy(e => e.DueDate).ToList());
    }

    private static string Days(int n) => n == 1 ? "1 day" : $"{n} days";

    private static DateOnly LocalDate(DateTimeOffset at, TimeZoneInfo zone) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, zone).DateTime);

    private static DateTimeOffset Utc(DateOnly localDate, TimeZoneInfo zone) =>
        new(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified), zone), TimeSpan.Zero);

    private static DateOnly Min(DateOnly a, DateOnly b) => a < b ? a : b;
}
