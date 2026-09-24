using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Audit;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Clinical;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Tenancy;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;

namespace PhysioTrac.Infrastructure.Services;

public class HomeExerciseProgramService : IHomeExerciseProgramService
{
    private readonly PhysioTracDbContext _db;
    private readonly ITenantAccessService _tenantAccess;
    private readonly IAuditService _audit;

    public HomeExerciseProgramService(PhysioTracDbContext db, ITenantAccessService tenantAccess, IAuditService audit)
    {
        _db = db;
        _tenantAccess = tenantAccess;
        _audit = audit;
    }

    public async Task<HomeExerciseProgram> CreateAsync(CreateHomeExerciseProgramRequest request, ICurrentUser actor, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, RoleSets.Clinical);
        var patient = await _tenantAccess.RequirePatientAccessAsync(actor, request.PatientId, ct: ct);
        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw new InvalidOperationException("A title is required.");
        }

        var program = new HomeExerciseProgram
        {
            OrganizationId = organization.Id,
            PatientId = patient.Id,
            CreatedById = actor.UserId,
            Title = request.Title.Trim(),
            GeneralInstructions = request.GeneralInstructions,
        };

        var order = 0;
        foreach (var item in request.Items)
        {
            if (string.IsNullOrWhiteSpace(item.Name)) continue;
            program.Items.Add(new HomeExerciseItem
            {
                Name = item.Name.Trim(),
                Description = item.Description,
                Sets = item.Sets,
                Reps = item.Reps,
                HoldSeconds = item.HoldSeconds,
                FrequencyPerDay = item.FrequencyPerDay,
                Notes = item.Notes,
                MediaUrl = item.MediaUrl,
                Order = order++,
            });
        }

        _db.HomeExercisePrograms.Add(program);
        await _db.SaveChangesAsync(ct);

        await _audit.RecordAuditEventAsync(actor.UserId, "home_exercise_program.created", nameof(HomeExerciseProgram), program.Id,
            organization.Id, patientId: patient.Id, metadata: new { itemCount = program.Items.Count }, ct: ct);

        return program;
    }

    public async Task<HomeExerciseItem> AddItemAsync(Guid programId, CreateHomeExerciseItemRequest request, ICurrentUser actor, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, RoleSets.Clinical);
        var program = await LoadProgramInOrgAsync(programId, actor, ct);

        if (program.Status != HomeExerciseProgramStatus.Active)
        {
            throw new InvalidOperationException("Only an active program can have exercises added.");
        }
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new InvalidOperationException("An exercise name is required.");
        }

        var nextOrder = await _db.HomeExerciseItems.Where(i => i.ProgramId == program.Id)
            .Select(i => (int?)i.Order).MaxAsync(ct) ?? -1;

        var item = new HomeExerciseItem
        {
            ProgramId = program.Id,
            Name = request.Name.Trim(),
            Description = request.Description,
            Sets = request.Sets,
            Reps = request.Reps,
            HoldSeconds = request.HoldSeconds,
            FrequencyPerDay = request.FrequencyPerDay,
            Notes = request.Notes,
            MediaUrl = request.MediaUrl,
            Order = nextOrder + 1,
        };
        _db.HomeExerciseItems.Add(item);
        program.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        return item;
    }

    public async Task RemoveItemAsync(Guid programId, Guid itemId, ICurrentUser actor, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, RoleSets.Clinical);
        var program = await LoadProgramInOrgAsync(programId, actor, ct);

        var item = await _db.HomeExerciseItems.FirstOrDefaultAsync(i => i.Id == itemId && i.ProgramId == program.Id, ct)
            ?? throw new NotFoundException("Exercise item was not found.");

        _db.HomeExerciseItems.Remove(item);
        program.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<HomeExerciseProgram> DiscontinueAsync(Guid programId, ICurrentUser actor, CancellationToken ct = default)
    {
        _tenantAccess.RequireRole(actor, RoleSets.Clinical);
        var program = await LoadProgramInOrgAsync(programId, actor, ct);

        if (program.Status == HomeExerciseProgramStatus.Discontinued)
        {
            throw new InvalidOperationException("This program was already discontinued.");
        }

        program.Status = HomeExerciseProgramStatus.Discontinued;
        program.DiscontinuedAt = DateTimeOffset.UtcNow;
        program.DiscontinuedById = actor.UserId;
        program.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);
        await _audit.RecordAuditEventAsync(actor.UserId, "home_exercise_program.discontinued", nameof(HomeExerciseProgram), program.Id,
            organization.Id, patientId: program.PatientId, ct: ct);

        return program;
    }

    public async Task<IReadOnlyList<HomeExerciseProgram>> ListForPatientAsync(Guid patientId, ICurrentUser actor, CancellationToken ct = default)
    {
        var patient = await _tenantAccess.RequirePatientAccessAsync(actor, patientId, ct: ct);
        return await _db.HomeExercisePrograms
            .Include(p => p.Items)
            .Where(p => p.PatientId == patient.Id)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);
    }

    private async Task<HomeExerciseProgram> LoadProgramInOrgAsync(Guid programId, ICurrentUser actor, CancellationToken ct)
    {
        var organization = await _tenantAccess.OrganizationRequiredAsync(actor, ct);
        var program = await _db.HomeExercisePrograms.Include(p => p.Items).FirstOrDefaultAsync(p => p.Id == programId, ct)
            ?? throw new NotFoundException("Home exercise program was not found.");
        if (program.OrganizationId != organization.Id)
        {
            throw new NotFoundException("Home exercise program was not found.");
        }
        return program;
    }
}
