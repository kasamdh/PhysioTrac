using Microsoft.EntityFrameworkCore;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;
using PhysioTrac.Infrastructure.Services;

namespace PhysioTrac.Tests;

/// <summary>A small but realistic clinic for scheduling-rule and calendar
/// tests: an NC location (America/New_York), a licensed PT and PTA who both
/// work there Mon-Fri 08:00-17:00, and a second organization whose data
/// must never leak in. Times are built in clinic-local wall-clock time via
/// <see cref="At"/>, on a weekday safely in the future.</summary>
public sealed class SchedulingFixture
{
    public PhysioTracDbContext Db { get; }
    public AppointmentService Appointments { get; }
    public ScheduleService Schedule { get; }
    public Organization Org { get; }
    public Organization OtherOrg { get; }
    public Location Clinic { get; }
    public Location OtherClinic { get; }
    public Provider Pt { get; }
    public Provider Pta { get; }
    public Patient Patient { get; }
    public Patient OtherPatient { get; }
    public AppointmentType FollowUp { get; }
    public DateOnly Monday { get; }
    public TimeZoneInfo Tz { get; } = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

    public TestCurrentUser Scheduler { get; }
    public TestCurrentUser Admin { get; }

    public SchedulingFixture()
    {
        Db = new PhysioTracDbContext(new DbContextOptionsBuilder<PhysioTracDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        Org = new Organization { Name = "Org 1000", Slug = "org-1000", ClientNumber = 1000, Timezone = "America/New_York" };
        OtherOrg = new Organization { Name = "Org 1001", Slug = "org-1001", ClientNumber = 1001, Timezone = "America/New_York" };
        Clinic = new Location { OrganizationId = Org.Id, Name = "Main Clinic", State = "NC", Timezone = "America/New_York" };
        OtherClinic = new Location { OrganizationId = OtherOrg.Id, Name = "Elsewhere", State = "NC", Timezone = "America/New_York" };
        Patient = new Patient { OrganizationId = Org.Id, FirstName = "John", LastName = "Smith", DateOfBirth = new DateOnly(1980, 5, 17), Phone = "555-010-2233", MedicalRecordNumber = "PT10025" };
        OtherPatient = new Patient { OrganizationId = Org.Id, FirstName = "Mary", LastName = "Jones", DateOfBirth = new DateOnly(1975, 2, 3) };
        FollowUp = new AppointmentType { OrganizationId = Org.Id, Name = "Follow-Up", DefaultDurationMinutes = 30 };
        Db.Organizations.AddRange(Org, OtherOrg);
        Db.Locations.AddRange(Clinic, OtherClinic);
        Db.Patients.AddRange(Patient, OtherPatient);
        Db.AppointmentTypes.Add(FollowUp);
        Db.SaveChanges();

        Pt = AddProvider("Sarah", "Miller", ProviderDiscipline.PT);
        Pta = AddProvider("John", "Carter", ProviderDiscipline.PTA);

        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Tz).DateTime);
        Monday = today.AddDays(((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7 + 7); // a Monday 1-2 weeks out

        var audit = new AuditService(Db);
        var tenantAccess = new TenantAccessService(Db, audit);
        Appointments = new AppointmentService(Db, tenantAccess, audit, new RecordingReminderService());
        Schedule = new ScheduleService(Db, tenantAccess);
        Scheduler = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = Org.Id, Role = UserRole.Scheduler };
        Admin = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = Org.Id, Role = UserRole.Admin };
    }

    public Provider AddProvider(string first, string last, ProviderDiscipline discipline, bool withHours = true)
    {
        var provider = new Provider
        {
            OrganizationId = Org.Id,
            FirstName = first,
            LastName = last,
            Discipline = discipline,
            Credentials = discipline == ProviderDiscipline.PTA ? "PTA" : "PT, DPT",
            UserId = TestTherapists.Add(Db, Org.Id, discipline == ProviderDiscipline.PTA ? UserRole.Assistant : UserRole.Therapist),
            Licenses = { TestTherapists.ValidLicense("NC") },
        };
        provider.Locations.Add(Clinic);
        Db.Providers.Add(provider);
        if (withHours)
        {
            foreach (var day in new[] { Weekday.Monday, Weekday.Tuesday, Weekday.Wednesday, Weekday.Thursday, Weekday.Friday })
            {
                Db.ProviderAvailabilities.Add(new ProviderAvailability
                {
                    ProviderId = provider.Id,
                    LocationId = Clinic.Id,
                    DayOfWeek = day,
                    StartTime = new TimeOnly(8, 0),
                    EndTime = new TimeOnly(17, 0),
                });
            }
        }
        Db.SaveChanges();
        return provider;
    }

    /// <summary>Clinic-local wall-clock time on <paramref name="date"/> (Monday by default).</summary>
    public DateTimeOffset At(int hour, int minute = 0, DateOnly? date = null)
    {
        var local = (date ?? Monday).ToDateTime(new TimeOnly(hour, minute));
        return new DateTimeOffset(local, Tz.GetUtcOffset(local));
    }

    public Application.Scheduling.CreateAppointmentRequest Book(
        Provider provider, DateTimeOffset start, int minutes = 30, Patient? patient = null,
        AppointmentKind kind = AppointmentKind.FollowUp, Guid? appointmentTypeId = null, Guid? locationId = null, string? overrideReason = null) =>
        new((patient ?? Patient).Id, provider.UserId!.Value, provider.Id, locationId ?? Clinic.Id, null,
            appointmentTypeId ?? FollowUp.Id, kind, start, start.AddMinutes(minutes), false, null, overrideReason);

    public TestCurrentUser UserFor(Provider provider) => new()
    {
        UserId = provider.UserId!.Value,
        OrganizationId = Org.Id,
        Role = provider.Discipline == ProviderDiscipline.PTA ? UserRole.Assistant : UserRole.Therapist,
    };
}
