using PhysioTrac.Api.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.InMemory;

namespace PhysioTrac.Tests;

/// <summary>Locks in the one behavior this policy exists for: a patient-like
/// object logged with {@} structured destructuring never carries its PHI
/// fields into the sink, while non-PHI properties on the same object still
/// come through untouched.</summary>
public class PhiRedactingDestructuringPolicyTests
{
    private record LoggedPatient(string FirstName, string LastName, string Email, string Diagnoses, Guid Id, int Status);

    [Fact]
    public void StructuredLog_RedactsPhiFields_ButKeepsNonPhiFields()
    {
        using var sink = new InMemorySink();
        var logger = new LoggerConfiguration()
            .Destructure.With<PhiRedactingDestructuringPolicy>()
            .WriteTo.Sink(sink)
            .CreateLogger();

        var patient = new LoggedPatient("Taylor", "Brooks", "taylor@example.test", "ACL tear", Guid.NewGuid(), 1);
        logger.Information("Patient {@Patient} created", patient);

        var evt = Assert.Single(sink.LogEvents);
        var structured = Assert.IsType<StructureValue>(evt.Properties["Patient"]);
        var byName = structured.Properties.ToDictionary(p => p.Name, p => p.Value);

        Assert.Equal("[REDACTED]", ((ScalarValue)byName["FirstName"]).Value);
        Assert.Equal("[REDACTED]", ((ScalarValue)byName["LastName"]).Value);
        Assert.Equal("[REDACTED]", ((ScalarValue)byName["Email"]).Value);
        Assert.Equal("[REDACTED]", ((ScalarValue)byName["Diagnoses"]).Value);

        // Non-PHI fields on the very same object are untouched -- this isn't
        // a blanket "redact the whole object" policy, only named PHI fields.
        Assert.Equal(patient.Id, ((ScalarValue)byName["Id"]).Value);
        Assert.Equal(patient.Status, ((ScalarValue)byName["Status"]).Value);
    }

    private record PlainDto(Guid Id, int Count);

    [Fact]
    public void StructuredLog_LeavesNonPhiObjects_ToDefaultDestructuring()
    {
        using var sink = new InMemorySink();
        var logger = new LoggerConfiguration()
            .Destructure.With<PhiRedactingDestructuringPolicy>()
            .WriteTo.Sink(sink)
            .CreateLogger();

        var dto = new PlainDto(Guid.NewGuid(), 5);
        logger.Information("Dto {@Dto} processed", dto);

        var evt = Assert.Single(sink.LogEvents);
        var structured = Assert.IsType<StructureValue>(evt.Properties["Dto"]);
        var byName = structured.Properties.ToDictionary(p => p.Name, p => p.Value);
        Assert.Equal(dto.Count, ((ScalarValue)byName["Count"]).Value);
    }
}
