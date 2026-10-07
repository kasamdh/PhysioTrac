using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Tenancy;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;

namespace PhysioTrac.Api.Controllers;

/// <summary>Today's workflow: one provider's appointments for the clinic's
/// current day, each with its visit note's status, so a therapist can check
/// patients in and document visits from one screen.</summary>
[ApiController]
[Route("api/v1/workflow")]
[Authorize]
public class WorkflowController : ControllerBase
{
    private readonly ITenantAccessService _tenantAccess;
    private readonly ICurrentUser _currentUser;
    private readonly PhysioTracDbContext _db;

    public WorkflowController(ITenantAccessService tenantAccess, ICurrentUser currentUser, PhysioTracDbContext db)
    {
        _tenantAccess = tenantAccess;
        _currentUser = currentUser;
        _db = db;
    }

    /// <param name="providerId">Whose day to show. Defaults to the caller's
    /// own provider record. With role checks on, therapists and assistants
    /// always get their own day regardless of this value.</param>
    /// <param name="allProviders">Every provider's appointments (front desk / admin view).</param>
    /// <param name="date">Only for testing and support; the page always asks for today.</param>
    [HttpGet("today")]
    public async Task<IActionResult> Today(
        [FromQuery] Guid? providerId = null, [FromQuery] bool allProviders = false, [FromQuery] DateOnly? date = null)
    {
        try
        {
            _tenantAccess.RequireRole(_currentUser, RoleSets.Scheduling);
            var ct = HttpContext.RequestAborted;
            var organization = await _tenantAccess.OrganizationRequiredAsync(_currentUser, ct);
            var tz = TimeZoneInfo.TryFindSystemTimeZoneById(organization.Timezone, out var found) ? found : TimeZoneInfo.Utc;
            var day = date ?? DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz));
            var dayStart = StartOfDayUtc(day, tz);
            var dayEnd = StartOfDayUtc(day.AddDays(1), tz);

            var providers = await _db.Providers.Where(p => p.OrganizationId == organization.Id && p.IsActive)
                .OrderBy(p => p.LastName).ThenBy(p => p.FirstName)
                .Select(p => new WorkflowProviderDto(p.Id, p.FirstName + " " + p.LastName, p.Credentials, p.Discipline, p.UserId))
                .ToListAsync(ct);
            var myProviderId = providers.FirstOrDefault(p => p.UserId == _currentUser.UserId)?.Id;

            var ownDayOnly = AccessControl.Enabled && _currentUser.Role is UserRole.Therapist or UserRole.Assistant;
            if (ownDayOnly)
            {
                allProviders = false;
                providerId = myProviderId;
            }
            var selectedProviderId = allProviders ? null : providerId ?? myProviderId;

            var query = _db.Appointments
                .Where(a => a.Patient!.OrganizationId == organization.Id && a.StartsAt >= dayStart && a.StartsAt < dayEnd);
            if (!allProviders)
            {
                // No provider chosen and the caller has none of their own: an
                // empty day rather than everyone's.
                query = selectedProviderId is Guid pid ? query.Where(a => a.ProviderId == pid) : query.Where(_ => false);
            }

            var appointments = await query
                .Select(a => new
                {
                    a.Id,
                    a.StartsAt,
                    a.EndsAt,
                    a.Status,
                    a.Kind,
                    a.ProviderId,
                    a.PatientId,
                    PatientFirst = a.Patient!.FirstName,
                    PatientLast = a.Patient.LastName,
                    Mrn = a.Patient.MedicalRecordNumber,
                    TypeName = a.AppointmentType != null ? a.AppointmentType.Name : null,
                    TypeColor = a.AppointmentType != null ? a.AppointmentType.Color : null,
                    LocationName = a.LocationDetail != null ? a.LocationDetail.Name : a.Location,
                })
                .ToListAsync(ct);

            var ids = appointments.Select(a => a.Id).ToList();
            var notes = await _db.ClinicalNotes.Where(n => n.AppointmentId != null && ids.Contains(n.AppointmentId.Value))
                .Select(n => new { n.Id, AppointmentId = n.AppointmentId!.Value, n.Status, n.NoteType, n.CreatedAt })
                .ToListAsync(ct);
            var latestNote = notes.GroupBy(n => n.AppointmentId).ToDictionary(g => g.Key, g => g.MaxBy(n => n.CreatedAt)!);
            var providerNames = providers.ToDictionary(p => p.Id, p => p.Name);

            var rows = appointments
                .OrderBy(a => a.StartsAt)
                .Select(a =>
                {
                    latestNote.TryGetValue(a.Id, out var note);
                    return new WorkflowAppointmentDto(
                        a.Id, a.StartsAt, a.EndsAt, a.Status, a.Kind, a.TypeName, a.TypeColor,
                        a.PatientId, $"{a.PatientFirst} {a.PatientLast}", a.Mrn,
                        a.ProviderId, a.ProviderId is Guid p && providerNames.TryGetValue(p, out var n) ? n : null,
                        a.LocationName, NoteTypeFor(a.Kind),
                        note?.Id, note?.Status, note?.NoteType);
                })
                .ToList();

            return Ok(new WorkflowDayDto(day, organization.Timezone, providers, myProviderId,
                selectedProviderId, allProviders, ownDayOnly, rows));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
    }

    /// <summary>The note type a visit of this kind is documented with.</summary>
    internal static NoteType NoteTypeFor(AppointmentKind kind) => kind switch
    {
        AppointmentKind.Evaluation => NoteType.Evaluation,
        AppointmentKind.ReEvaluation => NoteType.ReEvaluation,
        AppointmentKind.Progress => NoteType.Progress,
        AppointmentKind.Discharge => NoteType.Discharge,
        _ => NoteType.Daily,
    };

    private static DateTimeOffset StartOfDayUtc(DateOnly day, TimeZoneInfo tz)
    {
        var local = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return new DateTimeOffset(local, tz.GetUtcOffset(local)).ToUniversalTime();
    }
}

public record WorkflowProviderDto(Guid Id, string Name, string? Credentials, ProviderDiscipline Discipline, Guid? UserId);

public record WorkflowAppointmentDto(
    Guid AppointmentId, DateTimeOffset StartsAt, DateTimeOffset EndsAt, AppointmentStatus Status, AppointmentKind Kind,
    string? AppointmentTypeName, string? AppointmentTypeColor,
    Guid PatientId, string PatientName, string MedicalRecordNumber,
    Guid? ProviderId, string? ProviderName, string? LocationName,
    NoteType SuggestedNoteType, Guid? NoteId, NoteStatus? NoteStatus, NoteType? NoteType);

public record WorkflowDayDto(
    DateOnly Date, string Timezone, IReadOnlyList<WorkflowProviderDto> Providers, Guid? MyProviderId,
    Guid? SelectedProviderId, bool AllProviders, bool OwnDayOnly, IReadOnlyList<WorkflowAppointmentDto> Appointments);
