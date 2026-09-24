using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Infrastructure.Persistence;

namespace PhysioTrac.Api.Controllers;

/// <summary>Read-only ICD-10-CM catalog browse/search -- shared reference
/// data across every tenant (see DiagnosisCode's own doc comment), so this
/// deliberately has no tenant scoping at all, unlike almost every other
/// controller in this Api.</summary>
[ApiController]
[Route("api/v1/diagnosis-codes")]
[Authorize]
public class DiagnosisCodesController : ControllerBase
{
    private readonly PhysioTracDbContext _db;

    public DiagnosisCodesController(PhysioTracDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string? q, [FromQuery] int limit = 25)
    {
        var query = _db.DiagnosisCodes.Where(c => c.IsBillable);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(c => c.Code.Contains(term) || c.Description.Contains(term));
        }

        var results = await query
            .OrderBy(c => c.Code)
            .Take(Math.Clamp(limit, 1, 100))
            .Select(c => new DiagnosisCodeDto(c.Id, c.Code, c.Description, c.IsBillable))
            .ToListAsync(HttpContext.RequestAborted);

        return Ok(results);
    }
}

public record DiagnosisCodeDto(Guid Id, string Code, string Description, bool IsBillable);
