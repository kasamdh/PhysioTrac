using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using PhysioTrac.Application.Clinical;

namespace PhysioTrac.Tests;

/// <summary>The React app scores outcome measures live from a checked-in
/// copy of the catalog (frontend/.../outcomes/definitions.fixture.json, used
/// by its scoring tests). This keeps that copy identical to the server's
/// catalog. Run with UPDATE_FIXTURES=1 to rewrite it after changing a measure.</summary>
public class OutcomeDefinitionsFixtureTests
{
    [Fact]
    public void FrontendFixture_MatchesTheCatalog()
    {
        var json = JsonSerializer.Serialize(OutcomeMeasureCatalog.All, new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });
        var path = FixturePath();
        if (Environment.GetEnvironmentVariable("UPDATE_FIXTURES") == "1") File.WriteAllText(path, json + "\n");

        Assert.True(File.Exists(path), $"Missing {path}; run the tests with UPDATE_FIXTURES=1.");
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(json), JsonNode.Parse(File.ReadAllText(path))),
            "definitions.fixture.json is out of date; run the tests with UPDATE_FIXTURES=1.");
    }

    private static string FixturePath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "frontend"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "frontend", "src", "features", "encounter", "outcomes", "definitions.fixture.json");
    }
}
