using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Audit;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Tenancy;
using PhysioTrac.Infrastructure.Persistence;

namespace PhysioTrac.Api.Controllers;

/// <summary>Administration › Logs: the organization's activity log (every
/// change, sign-in and screen opened), plus the endpoint the app calls to
/// record which screen a user opened.</summary>
[ApiController]
[Route("api/v1")]
[Authorize]
public class AuditLogsController : ControllerBase
{
    /// <summary>The widest date range one request may cover.</summary>
    public const int MaxRangeDays = 93;

    private readonly ITenantAccessService _tenantAccess;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _audit;
    private readonly PhysioTracDbContext _db;

    public AuditLogsController(ITenantAccessService tenantAccess, ICurrentUser currentUser, IAuditService audit, PhysioTracDbContext db)
    {
        _tenantAccess = tenantAccess;
        _currentUser = currentUser;
        _audit = audit;
        _db = db;
    }

    /// <param name="from">First day (organization time zone, inclusive); defaults to today.</param>
    /// <param name="to">Last day (inclusive); defaults to <paramref name="from"/>.</param>
    /// <param name="category">One of <see cref="AuditCategories.All"/>; omitted = every category.</param>
    /// <param name="search">Matches the description, user, patient, item type or IP address.</param>
    [HttpGet("audit-logs")]
    public async Task<IActionResult> List(
        [FromQuery] DateOnly? from = null, [FromQuery] DateOnly? to = null, [FromQuery] Guid? userId = null,
        [FromQuery] string? category = null, [FromQuery] string? search = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        try
        {
            _tenantAccess.RequireRole(_currentUser, RoleSets.AuditLogReview);
            var ct = HttpContext.RequestAborted;
            var organization = await _tenantAccess.OrganizationRequiredAsync(_currentUser, ct);
            var tz = TimeZoneInfo.TryFindSystemTimeZoneById(organization.Timezone, out var found) ? found : TimeZoneInfo.Utc;

            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz));
            var firstDay = from ?? today;
            var lastDay = to ?? firstDay;
            if (lastDay < firstDay) (firstDay, lastDay) = (lastDay, firstDay);
            if (lastDay.DayNumber - firstDay.DayNumber >= MaxRangeDays)
            {
                return UnprocessableEntity(new { detail = $"Choose a range of {MaxRangeDays} days or less." });
            }
            var start = StartOfDayUtc(firstDay, tz);
            var end = StartOfDayUtc(lastDay.AddDays(1), tz);

            var query = _db.AuditEvents.Where(a =>
                a.OrganizationId == organization.Id && a.CreatedAt >= start && a.CreatedAt < end
                // Rows written before sessions stopped being logged.
                && a.ObjectType != "UserSession");
            if (userId is not null) query = query.Where(a => a.ActorId == userId);

            // Projected, then sorted in memory: ORDER BY over rows carrying
            // nvarchar(max) metadata is slow on SQL Express's small memory grant.
            var events = await query
                .Select(a => new { a.Id, a.CreatedAt, a.ActorId, a.Action, a.ObjectType, a.ObjectId, a.PatientId, a.IpAddress, a.MetadataJson })
                .ToListAsync(ct);

            var actorIds = events.Where(e => e.ActorId != null).Select(e => e.ActorId!.Value).Distinct().ToList();
            var actors = await _db.Users.Where(u => actorIds.Contains(u.Id))
                .Select(u => new { u.Id, u.FirstName, u.LastName, u.UserName })
                .ToDictionaryAsync(u => u.Id, ct);
            var patientIds = events.Where(e => e.PatientId != null).Select(e => e.PatientId!.Value).Distinct().ToList();
            var patients = await _db.Patients.Where(p => patientIds.Contains(p.Id))
                .Select(p => new { p.Id, p.FirstName, p.LastName, p.MedicalRecordNumber })
                .ToDictionaryAsync(p => p.Id, ct);

            var rows = events
                .Select(e =>
                {
                    var actor = e.ActorId is Guid aid && actors.TryGetValue(aid, out var a) ? a : null;
                    var patient = e.PatientId is Guid pid && patients.TryGetValue(pid, out var p) ? p : null;
                    return new AuditLogRowDto(
                        e.Id, e.CreatedAt, e.ActorId,
                        actor is null ? (e.ActorId is null ? "System" : "Unknown user") : $"{actor.FirstName} {actor.LastName}".Trim(),
                        actor?.UserName,
                        e.Action, AuditCategories.Of(e.Action, e.ObjectType), AuditDescriber.Describe(e.Action, e.ObjectType, e.MetadataJson),
                        e.ObjectType, e.ObjectId, e.PatientId,
                        patient is null ? null : $"{patient.FirstName} {patient.LastName}", patient?.MedicalRecordNumber,
                        e.IpAddress, e.MetadataJson);
                })
                .Where(r => category is null || r.Category == category)
                .Where(r => string.IsNullOrWhiteSpace(search) || Matches(r, search.Trim()))
                .OrderByDescending(r => r.At)
                .ToList();

            var size = Math.Clamp(pageSize, 1, 5000);
            var current = Math.Max(page, 1);
            var users = await _db.Users.Where(u => u.OrganizationId == organization.Id)
                .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
                .Select(u => new AuditLogUserDto(u.Id, (u.FirstName + " " + u.LastName).Trim(), u.UserName))
                .ToListAsync(ct);

            return Ok(new AuditLogPageDto(
                rows.Skip((current - 1) * size).Take(size).ToList(), rows.Count, current, size,
                firstDay, lastDay, organization.Timezone, users));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
    }

    /// <summary>Records that the signed-in user opened a screen ("system
    /// usage"). Only the path is kept -- never the query string, which can
    /// hold a patient search.</summary>
    [HttpPost("activity/page-view")]
    public async Task<IActionResult> PageView([FromBody] PageViewRequest request)
    {
        try
        {
            _tenantAccess.RequireRole(_currentUser, RoleSets.AllStaff);
            var path = (request.Path ?? string.Empty).Split('?', '#')[0];
            if (path.Length == 0 || path.Length > 200 || !path.StartsWith('/') || !SafePath.IsMatch(path))
            {
                return BadRequest(new { detail = "A page path like /schedule is required." });
            }
            var organization = await _tenantAccess.OrganizationRequiredAsync(_currentUser, HttpContext.RequestAborted);
            await _audit.RecordAuditEventAsync(
                _currentUser.UserId, "page.viewed", "Page", null, organization.Id,
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
                metadata: new { path }, ct: HttpContext.RequestAborted);
            return NoContent();
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
    }

    private static readonly Regex SafePath = new("^/[A-Za-z0-9/_-]*$", RegexOptions.Compiled);

    private static bool Matches(AuditLogRowDto r, string term) =>
        r.Description.Contains(term, StringComparison.OrdinalIgnoreCase)
        || r.UserName.Contains(term, StringComparison.OrdinalIgnoreCase)
        || (r.UserLogin?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
        || (r.PatientName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
        || (r.PatientMrn?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
        || r.ObjectType.Contains(term, StringComparison.OrdinalIgnoreCase)
        || r.Action.Contains(term, StringComparison.OrdinalIgnoreCase)
        || (r.IpAddress?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false);

    private static DateTimeOffset StartOfDayUtc(DateOnly day, TimeZoneInfo tz)
    {
        var local = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return new DateTimeOffset(local, tz.GetUtcOffset(local)).ToUniversalTime();
    }
}

/// <summary>Plain-language grouping of log entries for the Logs filter.</summary>
public static class AuditCategories
{
    public const string SignIn = "sign-in";
    public const string PageViews = "pages";
    public const string Patients = "patients";
    public const string Schedule = "schedule";
    public const string Notes = "notes";
    public const string Messages = "messages";
    public const string Billing = "billing";
    public const string Administration = "admin";
    public const string Other = "other";

    public static readonly string[] All = [SignIn, PageViews, Patients, Schedule, Notes, Messages, Billing, Administration, Other];

    private static readonly HashSet<string> ScheduleTypes = new(StringComparer.Ordinal)
    {
        "Appointment", "AppointmentSeries", "AppointmentType", "ProviderAvailability", "ProviderTimeOff",
        "LocationClosure", "Room", "Waitlist", "BookingConfiguration", "ProviderAppointmentType",
    };
    private static readonly HashSet<string> NoteTypes = new(StringComparer.Ordinal)
    {
        "ClinicalNote", "NoteAddendum", "NoteIntervention", "OutcomeScore", "ClinicalNoteTemplate", "FunctionalGoal",
    };
    private static readonly HashSet<string> BillingTypes = new(StringComparer.Ordinal)
    {
        "Charge", "Claim", "ClaimTransaction", "ClaimDenial", "Payer", "PayerFeeScheduleItem", "ServicePrice",
        "Superbill", "PatientPayment", "PaymentRecord", "PatientStatement", "PatientInsurance",
    };
    private static readonly HashSet<string> AdminTypes = new(StringComparer.Ordinal)
    {
        "ApplicationUser", "Location", "Provider", "ProviderLicense", "Organization", "ReferringProvider",
        "ConsentTemplate", "IntakeFormTemplate", "ClientInvitation",
    };

    public static string Of(string action, string objectType)
    {
        if (action.StartsWith("auth.", StringComparison.Ordinal)) return SignIn;
        if (action == "page.viewed") return PageViews;
        if (action.StartsWith("message.", StringComparison.Ordinal) || objectType == "Message") return Messages;
        if (action.StartsWith("note.", StringComparison.Ordinal) || NoteTypes.Contains(objectType)) return Notes;
        if (action.StartsWith("appointment", StringComparison.Ordinal) || action.StartsWith("schedule.", StringComparison.Ordinal)
            || action.StartsWith("provider.", StringComparison.Ordinal) || action.StartsWith("waitlist.", StringComparison.Ordinal)
            || action.StartsWith("room.", StringComparison.Ordinal) || ScheduleTypes.Contains(objectType)) return Schedule;
        if (BillingTypes.Contains(objectType) || action.StartsWith("superbill.", StringComparison.Ordinal)
            || action.StartsWith("payment", StringComparison.Ordinal) || action.StartsWith("patient_payment.", StringComparison.Ordinal)) return Billing;
        if (action.StartsWith("user.", StringComparison.Ordinal) || action.StartsWith("client", StringComparison.Ordinal)
            || action.StartsWith("privileged_access.", StringComparison.Ordinal) || AdminTypes.Contains(objectType)) return Administration;
        if (objectType.StartsWith("Patient", StringComparison.Ordinal) || action.StartsWith("patient", StringComparison.Ordinal)
            || action.StartsWith("consent", StringComparison.Ordinal) || action.StartsWith("intake_form", StringComparison.Ordinal)
            || action.StartsWith("goal.", StringComparison.Ordinal) || action.StartsWith("home_exercise", StringComparison.Ordinal)
            || action == "access.denied") return Patients;
        return Other;
    }
}

/// <summary>Turns a raw audit action into a sentence a clinic manager can read.</summary>
public static class AuditDescriber
{
    private static readonly Dictionary<string, string> Known = new(StringComparer.Ordinal)
    {
        ["auth.login.success"] = "Signed in",
        ["auth.login.failed"] = "Failed sign-in attempt",
        ["auth.logout"] = "Signed out",
        ["auth.password.changed"] = "Changed their own password",
        ["access.denied"] = "Was refused access",
        ["patient.viewed"] = "Opened a patient chart",
        ["user.invited"] = "Created a user (invitation)",
        ["user.updated"] = "Edited a user",
        ["user.password_reset"] = "Reset a user's password",
        ["user.role_changed"] = "Changed a user's access level",
        ["user.deactivated"] = "Deactivated a user",
        ["user.activated"] = "Reactivated a user",
        ["appointment.created"] = "Booked an appointment",
        ["appointment.confirmed"] = "Confirmed an appointment",
        ["appointment.checked_in"] = "Checked a patient in",
        ["appointment.started"] = "Started a visit",
        ["appointment.completed"] = "Completed an appointment",
        ["appointment.cancelled"] = "Cancelled an appointment",
        ["appointment.no_show"] = "Marked a no-show",
        ["appointment.rescheduled"] = "Rescheduled an appointment",
        ["appointment.moved"] = "Moved an appointment",
        ["appointment.provider_changed"] = "Changed an appointment's provider",
        ["appointment_series.created"] = "Booked a recurring series",
        ["appointment_series.cancelled"] = "Cancelled a recurring series",
        ["schedule.conflict_override"] = "Overrode a scheduling conflict",
        ["provider.weekly_hours_updated"] = "Changed a provider's working hours",
        ["provider.time_off_created"] = "Added provider time off",
        ["provider.time_off_cancelled"] = "Removed provider time off",
        ["note.created"] = "Started a clinical note",
        ["note.signed"] = "Signed a clinical note",
        ["note.signature_reauth_failed"] = "Failed the password check when signing a note",
        ["note.submitted_for_cosign"] = "Signed a note (sent for cosign)",
        ["note.cosigned"] = "Cosigned a clinical note",
        ["note.locked"] = "Locked a clinical note",
        ["note.addendum_created"] = "Added an addendum to a note",
        ["note.poc_certified"] = "Certified a plan of care",
        ["note.viewed"] = "Opened a clinical note",
        ["note.updated"] = "Edited a draft clinical note",
        ["note.printed"] = "Printed a clinical note",
        ["note.exported"] = "Exported a clinical note (PDF)",
        ["note.review_started"] = "Started reviewing a note",
        ["note.returned_for_correction"] = "Returned a note for correction",
        ["note.voided"] = "Voided a clinical note",
        ["note.amendment_started"] = "Started an amendment to a signed note",
        ["note.amended"] = "Amended a signed note",
        ["note.carry_forward"] = "Carried forward the previous visit's treatment",
        ["note.prefilled"] = "Prefilled a note from the episode",
        ["note.prefill_reviewed"] = "Reviewed prefilled note content",
        ["note.service_date_moved"] = "Moved a note's date of service with its visit",
        ["note.ai_draft_requested"] = "Asked AI to draft part of a note",
        ["note.ai_draft_inserted"] = "Inserted an AI draft into a note for review",
        ["patient_report.printed"] = "Printed a patient report",
        ["patient_report.exported"] = "Exported a patient report (PDF)",
        ["message.sent"] = "Sent a message",
        ["patient_document.uploaded"] = "Uploaded a patient document",
        ["patient_document.downloaded"] = "Downloaded a patient document",
        ["patient_document.deleted"] = "Deleted a patient document",
        ["consent.signed"] = "Recorded a signed consent",
        ["consent.revoked"] = "Revoked a consent",
    };

    public static string Describe(string action, string objectType, string? metadataJson)
    {
        if (Known.TryGetValue(action, out var text)) return text;
        var item = Humanize(objectType);
        switch (action)
        {
            case "page.viewed":
                return $"Opened {PageName(Read(metadataJson, "path"))}";
            case "entity.created":
                return $"Created {item}";
            case "entity.deleted":
                return $"Deleted {item}";
            case "entity.updated":
                var fields = ReadArray(metadataJson, "changedProperties");
                return fields.Length == 0 ? $"Updated {item}" : $"Updated {item}: {string.Join(", ", fields.Select(Humanize))}";
        }
        // Fallback: "patient_allergy.created" -> "Patient allergy created".
        var words = action.Replace('_', ' ').Replace('.', ' ');
        return char.ToUpperInvariant(words[0]) + words[1..];
    }

    /// <summary>"ProviderTimeOff" -> "provider time off".</summary>
    public static string Humanize(string name) =>
        Regex.Replace(name, "(?<=[a-z0-9])(?=[A-Z])", " ").ToLowerInvariant().Replace("application user", "user");

    private static string PageName(string? path) => path switch
    {
        null or "" => "a page",
        "/" => "Home",
        _ => string.Join(" › ", path.Trim('/').Split('/').Select(s => char.ToUpperInvariant(s[0]) + s[1..].Replace('-', ' '))),
    };

    private static string? Read(string? json, string name)
    {
        try
        {
            using var doc = JsonDocument.Parse(json ?? "{}");
            return doc.RootElement.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        }
        catch (JsonException) { return null; }
    }

    private static string[] ReadArray(string? json, string name)
    {
        try
        {
            using var doc = JsonDocument.Parse(json ?? "{}");
            return doc.RootElement.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Array
                ? v.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToArray()
                : [];
        }
        catch (JsonException) { return []; }
    }
}

public record PageViewRequest(string? Path);

public record AuditLogRowDto(
    Guid Id, DateTimeOffset At, Guid? UserId, string UserName, string? UserLogin,
    string Action, string Category, string Description,
    string ObjectType, Guid? ObjectId, Guid? PatientId, string? PatientName, string? PatientMrn,
    string? IpAddress, string MetadataJson);

public record AuditLogUserDto(Guid Id, string Name, string? UserName);

public record AuditLogPageDto(
    IReadOnlyList<AuditLogRowDto> Items, int Total, int Page, int PageSize,
    DateOnly From, DateOnly To, string Timezone, IReadOnlyList<AuditLogUserDto> Users);
