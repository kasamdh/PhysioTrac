using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Billing;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Tenancy;
using PhysioTrac.Domain.Entities;
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
