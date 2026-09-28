using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Identity;
using PhysioTrac.Infrastructure.Persistence;

namespace PhysioTrac.Infrastructure.Seed;

/// <summary>Development-only demo data for the staff calendar at Source
/// Motion: three bookable clinicians (two PTs and a PTA) with weekly hours,
/// daily lunch blocks, a few more fictional patients, and three weeks of
/// appointments around today in a realistic mix of statuses -- enough for
/// the Day view's provider columns and summaries to show something real.
///
/// Separate from DemoDataSeeder because that one only ever runs against an
/// empty database; this one also backfills an already-seeded one. It's
/// idempotent: it does nothing once any Source Motion provider has weekly
/// availability. Rows are written directly (not through AppointmentService)
/// and are consistent by construction -- inside working hours, outside
/// lunch, no patient or clinician overlaps.</summary>
public static class DemoSchedulingSeeder
{
    private sealed record Slot(int Hour, int Minute, int Minutes);

    private sealed record Shift(Weekday Day, Location Location, TimeOnly Start, TimeOnly End);

    public static async Task SeedAsync(
        PhysioTracDbContext db, UserManager<ApplicationUser> userManager, string demoPassword, CancellationToken ct = default)
    {
        var organization = await db.Organizations.FirstOrDefaultAsync(o => o.Slug == "source-motion-pt", ct);
        if (organization is null) return;

        var orgProviderIds = db.Providers.Where(p => p.OrganizationId == organization.Id).Select(p => p.Id);
        if (await db.ProviderAvailabilities.AnyAsync(a => orgProviderIds.Contains(a.ProviderId), ct)) return;

        var fuquay = await db.Locations.FirstAsync(l => l.OrganizationId == organization.Id && l.Name == "Fuquay-Varina", ct);
        var raleigh = await db.Locations.FirstAsync(l => l.OrganizationId == organization.Id && l.Name == "Raleigh", ct);
        var admin = await userManager.Users.FirstAsync(u => u.OrganizationId == organization.Id && u.Role == UserRole.Admin, ct);
        var tz = TimeZoneInfo.FindSystemTimeZoneById(fuquay.Timezone);

        // ---- Clinicians ----
        var jamieUser = await userManager.FindByNameAsync("therapist")
            ?? throw new InvalidOperationException("Demo user 'therapist' is missing.");
        var jamie = await db.Providers.Include(p => p.Locations).FirstAsync(p => p.UserId == jamieUser.Id, ct);
        jamie.Discipline = ProviderDiscipline.PT;

        var averyUser = await userManager.FindByNameAsync("assistant")
            ?? throw new InvalidOperationException("Demo user 'assistant' is missing.");
        var avery = await db.Providers.Include(p => p.Locations).FirstOrDefaultAsync(p => p.UserId == averyUser.Id, ct)
            ?? NewProvider(db, organization.Id, averyUser, "PTA", ProviderDiscipline.PTA, "Orthopedics", "NC-PTA-40317", fuquay, raleigh);

        var sofiaUser = await userManager.FindByNameAsync("therapist2")
            ?? await CreateUserAsync(userManager, organization.Id, "therapist2", "therapist2@sourcemotionpt.test",
                "Sofia", "Ramirez", UserRole.Therapist, demoPassword, "PT, DPT");
        var sofia = await db.Providers.Include(p => p.Locations).FirstOrDefaultAsync(p => p.UserId == sofiaUser.Id, ct)
            ?? NewProvider(db, organization.Id, sofiaUser, "PT, DPT", ProviderDiscipline.PT, "Sports Physical Therapy", "NC-PT-51902", fuquay);
        await db.SaveChangesAsync(ct);

        // ---- Weekly hours ----
        var weekdays = new[] { Weekday.Monday, Weekday.Tuesday, Weekday.Wednesday, Weekday.Thursday, Weekday.Friday };
        var shifts = new Dictionary<Guid, List<Shift>>
        {
            [jamie.Id] = weekdays.Select(d => new Shift(d, d is Weekday.Tuesday or Weekday.Thursday ? raleigh : fuquay, new(8, 0), new(17, 0))).ToList(),
            [sofia.Id] = weekdays.Select(d => new Shift(d, fuquay, new(7, 30), new(16, 0))).ToList(),
            [avery.Id] = weekdays.Select(d => d == Weekday.Friday
                ? new Shift(d, raleigh, new(9, 0), new(13, 0))
                : new Shift(d, fuquay, new(9, 0), new(18, 0))).ToList(),
        };
        foreach (var (providerId, list) in shifts)
        {
            db.ProviderAvailabilities.AddRange(list.Select(s => new ProviderAvailability
            {
                ProviderId = providerId,
                LocationId = s.Location.Id,
                DayOfWeek = s.Day,
                StartTime = s.Start,
                EndTime = s.End,
            }));
        }

        // ---- Patients ----
        var newPatients = new (string First, string Last, DateOnly Dob)[]
        {
            ("Marcus", "Webb", new(1968, 3, 14)), ("Lena", "Okafor", new(1991, 7, 2)), ("Daniel", "Price", new(1983, 11, 21)),
            ("Priya", "Nair", new(1996, 1, 9)), ("Owen", "Gallagher", new(1959, 9, 30)), ("Sara", "Lindqvist", new(1978, 5, 5)),
            ("Mateo", "Cruz", new(2004, 12, 12)), ("Grace", "Holloway", new(1987, 2, 27)),
        };
        var existingNames = await db.Patients.Where(p => p.OrganizationId == organization.Id)
            .Select(p => p.FirstName + " " + p.LastName).ToListAsync(ct);
        db.Patients.AddRange(newPatients.Where(p => !existingNames.Contains($"{p.First} {p.Last}")).Select((p, i) => new Patient
        {
            OrganizationId = organization.Id,
            FirstName = p.First,
            LastName = p.Last,
            DateOfBirth = p.Dob,
            Phone = $"919-555-02{i + 10:00}",
            Email = $"{p.First.ToLowerInvariant()}.{p.Last.ToLowerInvariant()}@example.test",
            PreferredLanguage = "English",
            PrimaryLocationId = fuquay.Id,
            AssignedTherapistId = i % 2 == 0 ? jamieUser.Id : sofiaUser.Id,
        }));
        await db.SaveChangesAsync(ct);

        // Only book the demo patients -- the four DemoDataSeeder creates plus
        // the ones above -- never charts someone added by hand.
        var demoNames = newPatients.Select(p => $"{p.First} {p.Last}")
            .Concat(["Taylor Brooks", "Riley Simmons", "Harper Ellison", "Quinn Alvarez"]).ToList();
        var patients = (await db.Patients.Where(p => p.OrganizationId == organization.Id && p.DeletedAt == null).ToListAsync(ct))
            .Where(p => demoNames.Contains(p.FullName)).OrderBy(p => p.LastName).ToList();
        var evalType = await db.AppointmentTypes.FirstAsync(t => t.OrganizationId == organization.Id && t.Name == "Initial Evaluation", ct);
        var followUpType = await db.AppointmentTypes.FirstAsync(t => t.OrganizationId == organization.Id && t.Name == "Follow-up Visit", ct);

        // ---- Lunch, time off, and appointments: the week before through two weeks after this one ----
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz).DateTime);
        var thisMonday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var firstDay = thisMonday.AddDays(-7);
        var lastDay = thisMonday.AddDays(20);
        DateTimeOffset At(DateOnly date, TimeOnly time)
        {
            var local = date.ToDateTime(time);
            return new DateTimeOffset(local, tz.GetUtcOffset(local));
        }

        var timeOff = new List<ProviderTimeOff>();
        for (var date = firstDay; date <= lastDay; date = date.AddDays(1))
        {
            foreach (var (providerId, list) in shifts)
            {
                var shift = list.FirstOrDefault(s => s.Day == ToWeekday(date));
                if (shift is null || shift.End <= new TimeOnly(13, 0)) continue;
                timeOff.Add(new ProviderTimeOff { ProviderId = providerId, LocationId = shift.Location.Id, StartDateTime = At(date, new(12, 0)), EndDateTime = At(date, new(13, 0)), Reason = TimeOffReason.Lunch });
            }
        }
        var nextFriday = thisMonday.AddDays(11);
        timeOff.Add(new ProviderTimeOff { ProviderId = sofia.Id, StartDateTime = At(nextFriday, new(13, 0)), EndDateTime = At(nextFriday, new(16, 0)), Reason = TimeOffReason.Personal, Notes = "Appointment" });
        db.ProviderTimeOffs.AddRange(timeOff);

        var patterns = new Dictionary<Guid, Slot[]>
        {
            [jamie.Id] = [new(8, 0, 60), new(9, 0, 30), new(9, 30, 30), new(10, 0, 60), new(11, 0, 60), new(13, 0, 60), new(14, 0, 30), new(14, 30, 30), new(15, 30, 60)],
            [sofia.Id] = [new(7, 30, 60), new(8, 30, 30), new(9, 0, 60), new(10, 30, 60), new(11, 30, 30), new(13, 0, 30), new(13, 30, 60), new(14, 30, 60)],
            [avery.Id] = [new(9, 0, 30), new(9, 30, 30), new(10, 0, 60), new(11, 0, 30), new(13, 0, 60), new(14, 0, 30), new(15, 0, 60), new(16, 0, 30), new(17, 0, 30)],
        };
        var providers = new[] { jamie, sofia, avery };

        var patientIds = patients.Select(p => p.Id).ToList();
        var busy = (await db.Appointments
                .Where(a => patientIds.Contains(a.PatientId) && a.Status != AppointmentStatus.Cancelled && a.Status != AppointmentStatus.NoShow)
                .Select(a => new { a.PatientId, a.StartsAt, a.EndsAt }).ToListAsync(ct))
            .Select(a => (a.PatientId, a.StartsAt, a.EndsAt)).ToList();

        var now = DateTimeOffset.UtcNow;
        var rotation = 0;
        for (var date = firstDay; date <= lastDay; date = date.AddDays(1))
        {
            var dayIndex = date.DayNumber - firstDay.DayNumber;
            for (var p = 0; p < providers.Length; p++)
            {
                var provider = providers[p];
                var shift = shifts[provider.Id].FirstOrDefault(s => s.Day == ToWeekday(date));
                if (shift is null) continue;

                var slots = patterns[provider.Id];
                for (var s = 0; s < slots.Length; s++)
                {
                    var slot = slots[s];
                    var startTime = new TimeOnly(slot.Hour, slot.Minute);
                    var endTime = startTime.AddMinutes(slot.Minutes);
                    if (startTime < shift.Start || endTime > shift.End) continue;
                    var start = At(date, startTime);
                    var end = start.AddMinutes(slot.Minutes);
                    if (timeOff.Any(t => t.ProviderId == provider.Id && t.StartDateTime < end && t.EndDateTime > start)) continue;
                    var hash = dayIndex * 31 + s * 7 + p * 3;
                    if ((dayIndex * 3 + s * 5 + p * 2) % 7 == 0) continue; // leave some open slots

                    Patient? patient = null;
                    for (var attempt = 0; attempt < patients.Count && patient is null; attempt++)
                    {
                        var candidate = patients[(rotation + attempt) % patients.Count];
                        if (!busy.Any(b => b.PatientId == candidate.Id && b.StartsAt < end && b.EndsAt > start)) patient = candidate;
                    }
                    rotation++;
                    if (patient is null) continue;
                    busy.Add((patient.Id, start, end));

                    var isEvaluation = provider.Discipline == ProviderDiscipline.PT && s == 0 && slot.Minutes == 60;
                    var status = end <= now
                        ? hash % 13 == 0 ? AppointmentStatus.NoShow : hash % 11 == 0 ? AppointmentStatus.Cancelled : AppointmentStatus.Completed
                        : start <= now ? AppointmentStatus.InProgress
                        : date == today && start - now <= TimeSpan.FromMinutes(30) ? AppointmentStatus.CheckedIn
                        : hash % 17 == 0 ? AppointmentStatus.Cancelled
                        : hash % 3 == 0 ? AppointmentStatus.Confirmed
                        : AppointmentStatus.Scheduled;

                    db.Appointments.Add(new Appointment
                    {
                        PatientId = patient.Id,
                        TherapistId = provider.UserId!.Value,
                        ProviderId = provider.Id,
                        LocationDetailId = shift.Location.Id,
                        AppointmentTypeId = isEvaluation ? evalType.Id : followUpType.Id,
                        Kind = isEvaluation ? AppointmentKind.Evaluation : AppointmentKind.FollowUp,
                        Status = status,
                        ConfirmedAt = status == AppointmentStatus.Confirmed ? start.AddDays(-2) : null,
                        StartsAt = start,
                        EndsAt = end,
                        BookingSource = BookingSource.FrontDesk,
                        ReasonForVisit = isEvaluation ? "New patient evaluation" : null,
                        CreatedById = admin.Id,
                    });
                }
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private static Provider NewProvider(
        PhysioTracDbContext db, Guid organizationId, ApplicationUser user, string credentials, ProviderDiscipline discipline,
        string specialty, string licenseNumber, params Location[] locations)
    {
        var provider = new Provider
        {
            OrganizationId = organizationId,
            UserId = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Credentials = credentials,
            Discipline = discipline,
            Specialty = specialty,
        };
        foreach (var location in locations) provider.Locations.Add(location);
        provider.Licenses.Add(new ProviderLicense
        {
            State = "NC",
            LicenseNumber = licenseNumber,
            IssueDate = new DateOnly(2021, 5, 1),
            ExpirationDate = DateOnly.FromDateTime(DateTime.Today.AddYears(2)),
            Status = ProviderLicenseStatus.Active,
        });
        db.Providers.Add(provider);
        return provider;
    }

    private static Weekday ToWeekday(DateOnly date) => (Weekday)(((int)date.DayOfWeek + 6) % 7);

    private static async Task<ApplicationUser> CreateUserAsync(
        UserManager<ApplicationUser> userManager, Guid organizationId, string userName, string email,
        string firstName, string lastName, UserRole role, string demoPassword, string? credential)
    {
        var user = new ApplicationUser
        {
            UserName = userName,
            Email = email,
            EmailConfirmed = true,
            OrganizationId = organizationId,
            FirstName = firstName,
            LastName = lastName,
            Role = role,
            Credential = credential,
            MustUseMfa = false,
        };
        var result = await userManager.CreateAsync(user, demoPassword);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to seed demo user '{userName}': {string.Join("; ", result.Errors.Select(e => e.Description))}");
        }
        return user;
    }
}
