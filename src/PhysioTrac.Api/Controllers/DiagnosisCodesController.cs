using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhysioTrac.Infrastructure.Persistence;

namespace PhysioTrac.Api.Controllers;

/// <summary>Read-only ICD-10-CM reference lookup — the same catalog for
/// every tenant. Maintained only via a seed process, never edited here.</summary>
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
    public async Task<IActionResult> Search([FromQuery(Name = "q")] string? query, [FromQuery] int limit = 25)
    {
        var q = _db.DiagnosisCodes.Where(d => d.IsBillable);
        if (!string.IsNullOrWhiteSpace(query))
        {
            var text = query.Trim();
            q = q.Where(d => d.Code.Contains(text) || d.Description.Contains(text));
        }
        var results = await q.OrderBy(d => d.Code).Take(Math.Clamp(limit, 1, 100)).ToListAsync(HttpContext.RequestAborted);
        return Ok(results);
    }
}
