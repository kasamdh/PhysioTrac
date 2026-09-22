using System.Text;
using System.Text.RegularExpressions;

namespace PhysioTrac.Infrastructure.Services;

internal static class Slug
{
    public static string Slugify(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        normalized = Regex.Replace(normalized, @"[^a-z0-9\s-]", "");
        normalized = Regex.Replace(normalized, @"[\s-]+", "-").Trim('-');
        return normalized;
    }
}
