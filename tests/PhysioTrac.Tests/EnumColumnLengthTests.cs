using Microsoft.EntityFrameworkCore;
using PhysioTrac.Infrastructure.Persistence;

namespace PhysioTrac.Tests;

/// <summary>Enums stored as their names (HasConversion&lt;string&gt;) must fit
/// their column. The in-memory test database doesn't enforce lengths, so
/// this checks the relational model directly -- "ReturnedForCorrection" (21
/// characters) once failed to save into a 20-character status column.</summary>
public class EnumColumnLengthTests
{
    [Fact]
    public void EveryEnumName_FitsItsColumn()
    {
        using var db = new PhysioTracDbContext(new DbContextOptionsBuilder<PhysioTracDbContext>()
            .UseSqlServer("Server=.;Database=model-only").Options);
        var problems = new List<string>();
        foreach (var entity in db.Model.GetEntityTypes())
            foreach (var property in entity.GetProperties())
            {
                var type = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
                if (!type.IsEnum || property.GetMaxLength() is not int max) continue;
                if (property.GetProviderClrType() != typeof(string) && property.GetValueConverter()?.ProviderClrType != typeof(string)) continue;
                foreach (var name in Enum.GetNames(type).Where(n => n.Length > max))
                    problems.Add($"{entity.ClrType.Name}.{property.Name}: \"{name}\" ({name.Length}) > {max}");
            }
        Assert.Empty(problems);
    }
}
