using Microsoft.EntityFrameworkCore;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;
using PhysioTrac.Infrastructure.Services;

namespace PhysioTrac.Infrastructure.Seed;

/// <summary>Keeps the system documentation templates in step with
/// <see cref="SystemTemplates"/>: creates missing ones, and publishes a new
/// version when a definition changed. Never edits or removes an existing
/// version. Safe to run at every start; contains no patient data.</summary>
public static class SystemTemplateSeeder
{
    public static async Task SeedAsync(PhysioTracDbContext db, CancellationToken ct = default) =>
        await SeedAsync(db, SystemTemplates.All, ct);

    public static async Task SeedAsync(PhysioTracDbContext db, IReadOnlyList<SystemTemplate> definitions, CancellationToken ct = default)
    {
        foreach (var def in definitions)
        {
            var template = await db.ClinicalNoteTemplates.FirstOrDefaultAsync(t => t.IsSystem && t.TemplateKey == def.Key, ct);
            if (template is null)
            {
                template = new ClinicalNoteTemplate
                {
                    TemplateKey = def.Key,
                    IsSystem = true,
                    Scope = TemplateScope.Platform,
                    Name = def.Name,
                    NoteType = def.NoteType,
                    Specialty = def.Specialty,
                    Description = def.Description,
                    Version = 1,
                };
                db.ClinicalNoteTemplates.Add(template);
                DocumentationTemplateService.AddVersion(db, template, 1, def.Sections, "Initial system version", null);
                continue;
            }

            template.Name = def.Name;
            template.NoteType = def.NoteType;
            template.Specialty = def.Specialty;
            template.Description = def.Description;

            var latest = await db.ClinicalNoteTemplateVersions.AsNoTracking().Include(v => v.Sections).Include(v => v.Fields)
                .Where(v => v.TemplateId == template.Id).OrderByDescending(v => v.VersionNumber).FirstOrDefaultAsync(ct);
            var current = latest is null ? null : DocumentationTemplateService.Canonical(DocumentationTemplateService.ToSectionDtos(latest));
            if (current != DocumentationTemplateService.Canonical(def.Sections))
            {
                template.Version = (latest?.VersionNumber ?? 0) + 1;
                DocumentationTemplateService.AddVersion(db, template, template.Version, def.Sections, "Updated system template", null);
            }
        }
        await db.SaveChangesAsync(ct);
    }
}
