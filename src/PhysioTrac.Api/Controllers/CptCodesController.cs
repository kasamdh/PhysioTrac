using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Infrastructure.Persistence;

namespace PhysioTrac.Api.Controllers;

/// <summary>Read-only CPT/HCPCS catalog browse/search -- the CPT-side twin
/// of DiagnosisCodesController: shared reference data across every tenant,
/// deliberately no tenant scoping, maintained only via a seed process.</summary>
[ApiController]
[Route("api/v1/cpt-codes")]
[Authorize]
public class CptCodesController : ControllerBase
{
    private readonly PhysioTracDbContext _db;

    public CptCodesController(PhysioTracDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string? q, [FromQuery] int limit = 25)
    {
        var query = _db.CptCodes.Where(c => c.IsActive);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(c => c.Code.Contains(term) || c.Description.Contains(term));
        }

        var results = await query
            .OrderBy(c => c.Code)
            .Take(Math.Clamp(limit, 1, 100))
            .Select(c => new CptCodeDto(c.Id, c.Code, c.Description, c.IsTimeBased, c.IsActive))
            .ToListAsync(HttpContext.RequestAborted);

        return Ok(results);
    }
}

public record CptCodeDto(Guid Id, string Code, string Description, bool IsTimeBased, bool IsActive);
