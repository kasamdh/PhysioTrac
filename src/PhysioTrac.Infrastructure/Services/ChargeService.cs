using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Billing;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Tenancy;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;

namespace PhysioTrac.Infrastructure.Services;

/// <summary>Direct port of `Charge.clean()`'s capture rules from `care/models.py`.</summary>
public class ChargeService : IChargeService
{
    private static readonly Regex CptCodePattern = new("^[A-Z0-9]{5}$");
    private static readonly Regex ModifierPattern = new("^[A-Z0-9]{2}$");

    private readonly PhysioTracDbContext _db;
    private readonly ITenantAccessService _tenantAccess;

    public ChargeService(PhysioTracDbContext db, ITenantAccessService tenantAccess)
    {
        _db = db;
        _tenantAccess = tenantAccess;
    }

    public async Task<Charge> CreateAsync(CreateChargeRequest request, ICurrentUser actor, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, Application.Tenancy.RoleSets.Billing);
        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);

        var patient = await _db.Patients.FirstOrDefaultAsync(p => p.Id == request.PatientId && p.OrganizationId == organization.Id, ct)
            ?? throw new NotFoundException("Patient was not found.");

        if (!CptCodePattern.IsMatch(request.CptCode))
        {
            throw new InvalidOperationException("Enter a 5-character CPT/HCPCS code (e.g. 97110).");
        }

        var modifiers = request.Modifiers ?? Array.Empty<string>();
        if (modifiers.Count > 4)
        {
            throw new InvalidOperationException("Enter at most 4 modifiers.");
        }
        foreach (var modifier in modifiers)
        {
            if (!ModifierPattern.IsMatch(modifier))
            {
                throw new InvalidOperationException("Modifiers must each be 2 characters (e.g. GP, 59).");
            }
        }

        if (request.ClinicalNoteId is Guid noteId)
        {
            var note = await _db.ClinicalNotes.FirstOrDefaultAsync(n => n.Id == noteId, ct)
                ?? throw new NotFoundException("The linked note was not found.");
            if (note.PatientId != patient.Id)
            {
                throw new InvalidOperationException("The linked note must belong to this patient.");
            }
        }

        if (request.LocationId is Guid locationId)
        {
            var locationExists = await _db.Locations.AnyAsync(l => l.Id == locationId && l.OrganizationId == organization.Id, ct);
            if (!locationExists)
            {
                throw new InvalidOperationException("Location must belong to the same organization.");
            }
        }

        // Always computed server-side — never client-supplied.
        var recommendedUnits = request.Minutes is int minutes ? EightMinuteRuleCalculator.ComputeUnits(minutes) : (int?)null;

        if (recommendedUnits is int recommended && request.Units != recommended && string.IsNullOrWhiteSpace(request.UnitsOverrideReason))
        {
            throw new InvalidOperationException("Explain why the entered units differ from the recommended units.");
        }

        var charge = new Charge
        {
            OrganizationId = organization.Id,
            PatientId = patient.Id,
            ClinicalNoteId = request.ClinicalNoteId,
            ProviderId = request.ProviderId,
            LocationId = request.LocationId,
            ServiceDate = request.ServiceDate,
            CptCode = request.CptCode,
            ModifiersJson = JsonSerializer.Serialize(modifiers),
            Units = request.Units,
            Minutes = request.Minutes,
            RecommendedUnits = recommendedUnits,
            UnitsOverrideReason = request.UnitsOverrideReason,
            ChargeAmount = request.ChargeAmount,
            CreatedById = actor.UserId,
        };

        if (request.DiagnosisCodeIds is { Count: > 0 })
        {
            var codes = await _db.DiagnosisCodes.Where(d => request.DiagnosisCodeIds.Contains(d.Id)).ToListAsync(ct);
            foreach (var code in codes) charge.DiagnosisCodes.Add(code);
        }

        _db.Charges.Add(charge);
        await _db.SaveChangesAsync(ct);
        return charge;
    }

    public async Task<Charge> UpdateStatusAsync(Guid chargeId, UpdateChargeStatusRequest request, ICurrentUser actor, CancellationToken ct = default)
    {
        var charge = await LoadChargeInOrgAsync(chargeId, actor, ct);
        _tenantAccess.RequireRole(actor, Application.Tenancy.RoleSets.Billing);

        charge.Status = request.Status;
        charge.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return charge;
    }

    public async Task<Charge> GetAsync(Guid chargeId, ICurrentUser actor, CancellationToken ct = default) =>
        await LoadChargeInOrgAsync(chargeId, actor, ct);

    public async Task<IReadOnlyList<Charge>> ListForPatientAsync(Guid patientId, ICurrentUser actor, CancellationToken ct = default)
    {
        var patient = await _tenantAccess.RequirePatientAccessAsync(actor, patientId, ct: ct);
        return await _db.Charges.Where(c => c.PatientId == patient.Id)
            .OrderByDescending(c => c.ServiceDate).ThenByDescending(c => c.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Charge>> GenerateFromNoteAsync(Guid noteId, ICurrentUser actor, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, RoleSets.Billing);
        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);

        var note = await _db.ClinicalNotes.Include(n => n.InterventionItems).Include(n => n.Patient).Include(n => n.Appointment)
            .FirstOrDefaultAsync(n => n.Id == noteId, ct)
            ?? throw new NotFoundException("Note was not found.");
        if (note.Patient is null || note.Patient.OrganizationId != organization.Id)
        {
            throw new NotFoundException("Note was not found.");
        }
        if (!note.IsSigned)
        {
            throw new InvalidOperationException("Only a signed note can generate charges.");
        }
        if (await _db.Charges.AnyAsync(c => c.ClinicalNoteId == note.Id, ct))
        {
            throw new InvalidOperationException("Charges have already been generated for this note.");
        }

        var provider = await _db.Providers.FirstOrDefaultAsync(p => p.UserId == note.TherapistId && p.OrganizationId == organization.Id, ct)
            ?? throw new NotFoundException("The treating provider record was not found.");

        var locationId = note.Appointment?.LocationDetailId ?? note.Patient.PrimaryLocationId;
        var diagnosisCodeIds = await _db.PatientDiagnoses
            .Where(d => d.PatientId == note.PatientId && d.ResolvedDate == null)
            .OrderByDescending(d => d.IsPrimary).Select(d => d.DiagnosisCodeId).Take(12).ToListAsync(ct);
        var diagnosisCodes = await _db.DiagnosisCodes.Where(d => diagnosisCodeIds.Contains(d.Id)).ToListAsync(ct);

        var generated = new List<Charge>();
        var groups = note.InterventionItems.Where(i => i.Category is not null).GroupBy(i => i.Category!.Value);

        foreach (var group in groups)
        {
            var mapping = await _db.CptCodeMappings.FirstOrDefaultAsync(
                m => m.OrganizationId == organization.Id && m.InterventionCategory == group.Key && m.IsActive, ct)
                ?? throw new InvalidOperationException(
                    $"No active CPT mapping is configured for {group.Key}. Configure one (see CptCodeMappingsController) before generating charges from this note.");

            var timedMinutes = group.Where(i => i.IsTimed).Sum(i => i.Minutes);
            int units;
            int? minutesForCharge;
            if (timedMinutes > 0)
            {
                units = EightMinuteRuleCalculator.ComputeUnits(timedMinutes, organization.EightMinuteRuleVariant);
                minutesForCharge = timedMinutes;
                if (units == 0) continue; // below the billable threshold -- not a chargeable line yet
            }
            else
            {
                units = group.Count();
                minutesForCharge = null;
            }

            var amount = await ResolveFeeScheduleAmountInternalAsync(organization.Id, mapping.CptCode, locationId, ct) ?? 0m;

            var charge = new Charge
            {
                OrganizationId = organization.Id,
                PatientId = note.PatientId,
                ClinicalNoteId = note.Id,
                ProviderId = provider.Id,
                LocationId = locationId,
                ServiceDate = note.ServiceDate,
                CptCode = mapping.CptCode,
                ModifiersJson = "[]",
                Units = units,
                Minutes = minutesForCharge,
                RecommendedUnits = minutesForCharge is not null ? units : null,
                ChargeAmount = amount,
                CreatedById = actor.UserId,
            };
            foreach (var code in diagnosisCodes) charge.DiagnosisCodes.Add(code);
            generated.Add(charge);
        }

        if (generated.Count == 0)
        {
            throw new InvalidOperationException("This note has no billable interventions to generate charges from.");
        }

        _db.Charges.AddRange(generated);
        await _db.SaveChangesAsync(ct);
        return generated;
    }

    public async Task<Charge> GenerateFromAppointmentAsync(Guid appointmentId, ICurrentUser actor, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, RoleSets.Billing);
        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);

        var appointment = await _db.Appointments.Include(a => a.AppointmentType).Include(a => a.Patient)
            .FirstOrDefaultAsync(a => a.Id == appointmentId, ct)
            ?? throw new NotFoundException("Appointment was not found.");
        if (appointment.Patient is null || appointment.Patient.OrganizationId != organization.Id)
        {
            throw new NotFoundException("Appointment was not found.");
        }
        if (appointment.Status != AppointmentStatus.Completed)
        {
            throw new InvalidOperationException("Only a completed appointment can generate a charge.");
        }
        if (await _db.Charges.AnyAsync(c => c.AppointmentId == appointment.Id, ct))
        {
            throw new InvalidOperationException("A charge has already been generated for this appointment.");
        }
        if (appointment.ProviderId is null)
        {
            throw new InvalidOperationException("This appointment has no assigned provider.");
        }
        var cptCode = appointment.AppointmentType?.DefaultCptCode;
        if (string.IsNullOrWhiteSpace(cptCode))
        {
            throw new InvalidOperationException(
                "This appointment type has no default CPT code configured. Configure one, or generate charges from the visit note instead.");
        }

        var amount = appointment.AppointmentType!.Price
            ?? await ResolveFeeScheduleAmountInternalAsync(organization.Id, cptCode, appointment.LocationDetailId, ct)
            ?? throw new InvalidOperationException("No price is configured for this appointment type or CPT code.");

        var diagnosisCodeIds = await _db.PatientDiagnoses
            .Where(d => d.PatientId == appointment.PatientId && d.ResolvedDate == null)
            .OrderByDescending(d => d.IsPrimary).Select(d => d.DiagnosisCodeId).Take(12).ToListAsync(ct);
        var diagnosisCodes = await _db.DiagnosisCodes.Where(d => diagnosisCodeIds.Contains(d.Id)).ToListAsync(ct);

        var charge = new Charge
        {
            OrganizationId = organization.Id,
            PatientId = appointment.PatientId,
            AppointmentId = appointment.Id,
            ProviderId = appointment.ProviderId.Value,
            LocationId = appointment.LocationDetailId,
            ServiceDate = DateOnly.FromDateTime(appointment.StartsAt.UtcDateTime),
            CptCode = cptCode,
            ModifiersJson = "[]",
            Units = 1,
            ChargeAmount = amount,
            CreatedById = actor.UserId,
        };
        foreach (var code in diagnosisCodes) charge.DiagnosisCodes.Add(code);

        _db.Charges.Add(charge);
        await _db.SaveChangesAsync(ct);
        return charge;
    }

    public async Task<decimal?> ResolveFeeScheduleAmountAsync(string cptCode, Guid? locationId, ICurrentUser actor, CancellationToken ct = default)
    {
        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);
        return await ResolveFeeScheduleAmountInternalAsync(organization.Id, cptCode, locationId, ct);
    }

    private async Task<decimal?> ResolveFeeScheduleAmountInternalAsync(Guid organizationId, string cptCode, Guid? locationId, CancellationToken ct)
    {
        if (locationId is Guid loc)
        {
            var locationPrice = await _db.ServicePrices.FirstOrDefaultAsync(
                s => s.OrganizationId == organizationId && s.LocationId == loc && s.CptCode == cptCode && s.IsActive, ct);
            if (locationPrice is not null) return locationPrice.Price;
        }

        var orgPrice = await _db.ServicePrices.FirstOrDefaultAsync(
            s => s.OrganizationId == organizationId && s.LocationId == null && s.CptCode == cptCode && s.IsActive, ct);
        return orgPrice?.Price;
    }

    private async Task<Charge> LoadChargeInOrgAsync(Guid chargeId, ICurrentUser actor, CancellationToken ct)
    {
        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);
        var charge = await _db.Charges.FirstOrDefaultAsync(c => c.Id == chargeId, ct)
            ?? throw new NotFoundException("Charge was not found.");
        if (charge.OrganizationId != organization.Id)
        {
            throw new NotFoundException("Charge was not found.");
        }
        return charge;
    }
}
