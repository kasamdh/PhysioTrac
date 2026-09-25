using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Common;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;
using PhysioTrac.Infrastructure.Services;

namespace PhysioTrac.Tests;

public class DashboardServiceTests
{
    private static (PhysioTracDbContext Db, DashboardService Service, Organization Org) NewService()
    {
        var db = new PhysioTracDbContext(
            new DbContextOptionsBuilder<PhysioTracDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        var org = new Organization { Name = "Client A", Slug = "client-a", ClientNumber = 1000 };
        db.Organizations.Add(org);
        db.SaveChanges();

        var audit = new AuditService(db);
        var tenantAccess = new TenantAccessService(db, audit);
        return (db, new DashboardService(db, tenantAccess), org);
    }

    private static TestCurrentUser Admin(Guid orgId) => new() { UserId = Guid.NewGuid(), OrganizationId = orgId, Role = UserRole.Admin };
    private static TestCurrentUser Therapist(Guid orgId, Guid userId) => new() { UserId = userId, OrganizationId = orgId, Role = UserRole.Therapist };
    private static TestCurrentUser Patient(Guid orgId) => new() { UserId = Guid.NewGuid(), OrganizationId = orgId, Role = UserRole.Patient };

    private static Patient NewPatient(Organization org, DateTimeOffset createdAt, Guid? assignedTherapistId = null, Guid? referringProviderId = null, Guid? locationId = null) => new()
    {
        OrganizationId = org.Id, FirstName = "Pat", LastName = "Patient", DateOfBirth = new DateOnly(1990, 1, 1),
        CreatedAt = createdAt, AssignedTherapistId = assignedTherapistId, ReferringProviderId = referringProviderId, PrimaryLocationId = locationId,
    };

    private static Appointment NewAppointment(Patient patient, Guid therapistId, Guid? providerId, AppointmentStatus status, DateTimeOffset startsAt, Guid? locationId = null) => new()
    {
        PatientId = patient.Id, TherapistId = therapistId, ProviderId = providerId, Status = status,
        StartsAt = startsAt, EndsAt = startsAt.AddMinutes(30), LocationDetailId = locationId, CreatedById = therapistId,
    };

    [Fact]
    public async Task GetNewPatients_CountsOnlyPatientsCreatedWithinRange()
    {
        var (db, service, org) = NewService();
        var today = DateTime.UtcNow.Date;
        db.Patients.AddRange(
            NewPatient(org, today.AddDays(-2)),
            NewPatient(org, today.AddDays(-2)),
            NewPatient(org, today.AddDays(-40))); // out of range
        await db.SaveChangesAsync();

        var result = await service.GetNewPatientsAsync(Admin(org.Id), DateOnly.FromDateTime(today.AddDays(-7)), DateOnly.FromDateTime(today));

        Assert.Equal(2, result.TotalNewPatients);
    }

    [Fact]
    public async Task GetNewPatients_TherapistRole_SeesOnlyOwnCaseload()
    {
        var (db, service, org) = NewService();
        var therapistId = Guid.NewGuid();
        var today = DateTime.UtcNow.Date;
        db.Patients.AddRange(
            NewPatient(org, today, assignedTherapistId: therapistId),
            NewPatient(org, today, assignedTherapistId: Guid.NewGuid())); // a different therapist's patient
        await db.SaveChangesAsync();

        var result = await service.GetNewPatientsAsync(Therapist(org.Id, therapistId), DateOnly.FromDateTime(today), DateOnly.FromDateTime(today));

        Assert.Equal(1, result.TotalNewPatients);
    }

    [Fact]
    public async Task GetNewPatients_PatientRole_ThrowsForbidden()
    {
        var (_, service, org) = NewService();
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.GetNewPatientsAsync(Patient(org.Id), DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow)));
    }

    [Fact]
    public async Task GetNewPatients_EndBeforeStart_Throws()
    {
        var (_, service, org) = NewService();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetNewPatientsAsync(Admin(org.Id), today, today.AddDays(-1)));
    }

    [Fact]
    public async Task GetCancellationsAndNoShows_ComputesRatesCorrectly()
    {
        var (db, service, org) = NewService();
        var patient = NewPatient(org, DateTime.UtcNow);
        db.Patients.Add(patient);
        await db.SaveChangesAsync();
        var therapistId = Guid.NewGuid();
        var today = DateTimeOffset.UtcNow;
        db.Appointments.AddRange(
            NewAppointment(patient, therapistId, null, AppointmentStatus.Completed, today),
            NewAppointment(patient, therapistId, null, AppointmentStatus.Completed, today),
            NewAppointment(patient, therapistId, null, AppointmentStatus.Cancelled, today),
            NewAppointment(patient, therapistId, null, AppointmentStatus.NoShow, today));
        await db.SaveChangesAsync();

        var result = await service.GetCancellationsAndNoShowsAsync(Admin(org.Id), DateOnly.FromDateTime(today.Date), DateOnly.FromDateTime(today.Date));

        Assert.Equal(4, result.TotalAppointments);
        Assert.Equal(2, result.Completed);
        Assert.Equal(25.0m, result.CancellationRate);
        Assert.Equal(25.0m, result.NoShowRate);
    }

    [Fact]
    public async Task GetProviderProductivity_TherapistRole_SeesOnlyOwnVisitsAndCharges()
    {
        var (db, service, org) = NewService();
        var patient = NewPatient(org, DateTime.UtcNow);
        db.Patients.Add(patient);
        var providerA = new Provider { OrganizationId = org.Id, FirstName = "Jamie", LastName = "Chen" };
        var providerB = new Provider { OrganizationId = org.Id, FirstName = "Avery", LastName = "Kim" };
        db.Providers.AddRange(providerA, providerB);
        await db.SaveChangesAsync();

        var therapistAUserId = Guid.NewGuid();
        var today = DateTimeOffset.UtcNow;
        db.Appointments.AddRange(
            NewAppointment(patient, therapistAUserId, providerA.Id, AppointmentStatus.Completed, today),
            NewAppointment(patient, Guid.NewGuid(), providerB.Id, AppointmentStatus.Completed, today));
        db.Charges.AddRange(
            new Charge { OrganizationId = org.Id, PatientId = patient.Id, ProviderId = providerA.Id, ServiceDate = DateOnly.FromDateTime(today.Date), CptCode = "97110", Units = 2, ChargeAmount = 65m },
            new Charge { OrganizationId = org.Id, PatientId = patient.Id, ProviderId = providerB.Id, ServiceDate = DateOnly.FromDateTime(today.Date), CptCode = "97110", Units = 3, ChargeAmount = 90m });
        await db.SaveChangesAsync();

        var result = await service.GetProviderProductivityAsync(
            Therapist(org.Id, therapistAUserId), DateOnly.FromDateTime(today.Date), DateOnly.FromDateTime(today.Date));

        var row = Assert.Single(result.Rows);
        Assert.Equal(providerA.Id, row.ProviderId);
        Assert.Equal(1, row.CompletedVisits);
        Assert.Equal(2, row.TotalUnits);
        Assert.Equal(65m, row.TotalBilled);
    }

    [Fact]
    public async Task GetProviderProductivity_AdminRole_SeesEveryProvider()
    {
        var (db, service, org) = NewService();
        var patient = NewPatient(org, DateTime.UtcNow);
        db.Patients.Add(patient);
        var providerA = new Provider { OrganizationId = org.Id, FirstName = "Jamie", LastName = "Chen" };
        var providerB = new Provider { OrganizationId = org.Id, FirstName = "Avery", LastName = "Kim" };
        db.Providers.AddRange(providerA, providerB);
        await db.SaveChangesAsync();
        var today = DateTimeOffset.UtcNow;
        db.Appointments.AddRange(
            NewAppointment(patient, Guid.NewGuid(), providerA.Id, AppointmentStatus.Completed, today),
            NewAppointment(patient, Guid.NewGuid(), providerB.Id, AppointmentStatus.Completed, today));
        await db.SaveChangesAsync();

        var result = await service.GetProviderProductivityAsync(Admin(org.Id), DateOnly.FromDateTime(today.Date), DateOnly.FromDateTime(today.Date));

        Assert.Equal(2, result.Rows.Count);
    }

    [Fact]
    public async Task GetVisitsAndRetention_PatientSeenInBothHalves_CountsAsRetained()
    {
        var (db, service, org) = NewService();
        var stayingPatient = NewPatient(org, DateTime.UtcNow);
        var oneTimePatient = NewPatient(org, DateTime.UtcNow);
        db.Patients.AddRange(stayingPatient, oneTimePatient);
        await db.SaveChangesAsync();

        var therapistId = Guid.NewGuid();
        var from = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-40));
        var to = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        // First half (day -40..-20ish): both patients visit.
        var firstHalfDate = from.ToDateTime(TimeOnly.MinValue).AddDays(2);
        // Second half: only stayingPatient returns.
        var secondHalfDate = to.ToDateTime(TimeOnly.MinValue).AddDays(-2);

        db.Appointments.AddRange(
            NewAppointment(stayingPatient, therapistId, null, AppointmentStatus.Completed, firstHalfDate),
            NewAppointment(oneTimePatient, therapistId, null, AppointmentStatus.Completed, firstHalfDate),
            NewAppointment(stayingPatient, therapistId, null, AppointmentStatus.Completed, secondHalfDate));
        await db.SaveChangesAsync();

        var result = await service.GetVisitsAndRetentionAsync(Admin(org.Id), from, to);

        Assert.Equal(2, result.EligiblePatients);
        Assert.Equal(1, result.RetainedPatients);
        Assert.Equal(50.0m, result.RetentionRate);
    }

    [Fact]
    public async Task GetReferralSources_GroupsByReferringProvider_AndReportsUnattributedSeparately()
    {
        var (db, service, org) = NewService();
        var referrer = new ReferringProvider { OrganizationId = org.Id, FirstName = "Nadia", LastName = "Farouk" };
        db.ReferringProviders.Add(referrer);
        await db.SaveChangesAsync();
        var today = DateTime.UtcNow;
        db.Patients.AddRange(
            NewPatient(org, today, referringProviderId: referrer.Id),
            NewPatient(org, today, referringProviderId: referrer.Id),
            NewPatient(org, today)); // no referral source
        await db.SaveChangesAsync();

        var result = await service.GetReferralSourcesAsync(Admin(org.Id), DateOnly.FromDateTime(today), DateOnly.FromDateTime(today));

        Assert.Equal(2, result.Rows.Count);
        var referrerRow = Assert.Single(result.Rows, r => r.ReferringProviderId == referrer.Id);
        Assert.Equal(2, referrerRow.NewPatientCount);
        var unattributedRow = Assert.Single(result.Rows, r => r.ReferringProviderId == null);
        Assert.Equal(1, unattributedRow.NewPatientCount);
    }

    [Fact]
    public async Task GetLocationPerformance_RequiresBillingRole()
    {
        var (_, service, org) = NewService();
        var therapist = Therapist(org.Id, Guid.NewGuid());
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.GetLocationPerformanceAsync(therapist, DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow)));
    }

    [Fact]
    public async Task GetLocationPerformance_AggregatesRevenueAndVisitsPerLocation()
    {
        var (db, service, org) = NewService();
        var location = new Location { OrganizationId = org.Id, Name = "Downtown" };
        db.Locations.Add(location);
        var patient = NewPatient(org, DateTime.UtcNow, locationId: location.Id);
        db.Patients.Add(patient);
        await db.SaveChangesAsync();
        var today = DateTimeOffset.UtcNow;
        db.Appointments.Add(NewAppointment(patient, Guid.NewGuid(), null, AppointmentStatus.Completed, today, location.Id));
        db.Charges.Add(new Charge { OrganizationId = org.Id, PatientId = patient.Id, ProviderId = Guid.NewGuid(), LocationId = location.Id, ServiceDate = DateOnly.FromDateTime(today.Date), CptCode = "97110", ChargeAmount = 65m });
        await db.SaveChangesAsync();

        var result = await service.GetLocationPerformanceAsync(Admin(org.Id), DateOnly.FromDateTime(today.Date), DateOnly.FromDateTime(today.Date));

        var row = Assert.Single(result.Rows);
        Assert.Equal(location.Id, row.LocationId);
        Assert.Equal(1, row.Visits);
        Assert.Equal(65m, row.Revenue);
        Assert.Equal(1, row.NewPatients);
    }

    [Fact]
    public async Task Dashboards_NeverSeeAnotherOrganizationsData()
    {
        var (db, service, org) = NewService();
        var otherOrg = new Organization { Name = "Client B", Slug = "client-b", ClientNumber = 1001 };
        db.Organizations.Add(otherOrg);
        var today = DateTime.UtcNow;
        db.Patients.Add(NewPatient(otherOrg, today));
        await db.SaveChangesAsync();

        var result = await service.GetNewPatientsAsync(Admin(org.Id), DateOnly.FromDateTime(today), DateOnly.FromDateTime(today));

        Assert.Equal(0, result.TotalNewPatients);
    }
}
