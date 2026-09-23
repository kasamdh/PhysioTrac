namespace PhysioTrac.Application.Configuration;

/// <summary>Controls the Development-only demo dataset (see DemoDataSeeder).
/// DemoPassword has no default on purpose: appsettings.Development.json is
/// committed to git, so the password can never live there as a literal --
/// it must come from an environment variable (Seed__DemoPassword) or user
/// secrets, and the seeder fails loudly on startup if it's unset rather
/// than silently falling back to a known value.</summary>
public class SeedOptions
{
    public const string SectionName = "Seed";

    public string? DemoPassword { get; set; }
}
