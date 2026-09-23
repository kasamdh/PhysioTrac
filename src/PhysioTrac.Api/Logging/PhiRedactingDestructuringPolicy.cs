using System.Collections.Concurrent;
using System.Reflection;
using Serilog.Core;
using Serilog.Events;

namespace PhysioTrac.Api.Logging;

/// <summary>Masks likely-PHI property values whenever an object is logged as
/// structured data (e.g. <c>Log.Information("Patient {@Patient} created", patient)</c>).
/// This is a best-effort safety net, not a compliance guarantee: it only
/// catches structured ("@") destructuring, not PHI interpolated directly
/// into a message string (e.g. <c>Log.Information($"Patient {patient.FirstName}...")`)
/// -- that still requires callers not to do it. Matching is by property
/// name only (case-insensitive substring), not by inspecting values, so it
/// stays cheap and works across every entity/DTO without per-type
/// registration.</summary>
public class PhiRedactingDestructuringPolicy : IDestructuringPolicy
{
    private static readonly string[] PhiPropertyNameFragments =
    [
        "firstname", "lastname", "fullname", "dateofbirth", "dob",
        "email", "phone", "ssn", "address", "diagnos", "medicalrecordnumber",
        "npi", "insurance", "subjective", "objective", "assessment",
    ];

    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> PropertyCache = new();

    public bool TryDestructure(object value, ILogEventPropertyValueFactory propertyValueFactory, out LogEventPropertyValue result)
    {
        var type = value.GetType();

        // Only intervene for plain reference types with public properties --
        // let Serilog's built-in policies handle primitives, collections,
        // and everything else as they normally would.
        if (!type.IsClass || type == typeof(string) || type.Namespace?.StartsWith("System", StringComparison.Ordinal) == true)
        {
            result = null!;
            return false;
        }

        var properties = PropertyCache.GetOrAdd(type, t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance));
        if (!properties.Any(p => IsLikelyPhi(p.Name)))
        {
            result = null!;
            return false;
        }

        var logProperties = new List<LogEventProperty>();
        foreach (var property in properties)
        {
            if (property.GetIndexParameters().Length > 0) continue;

            object? propertyValue;
            try
            {
                propertyValue = property.GetValue(value);
            }
            catch
            {
                continue;
            }

            var loggedValue = IsLikelyPhi(property.Name)
                ? new ScalarValue("[REDACTED]")
                : propertyValueFactory.CreatePropertyValue(propertyValue, destructureObjects: true);

            logProperties.Add(new LogEventProperty(property.Name, loggedValue));
        }

        result = new StructureValue(logProperties, type.Name);
        return true;
    }

    private static bool IsLikelyPhi(string propertyName)
    {
        var lower = propertyName.ToLowerInvariant();
        return PhiPropertyNameFragments.Any(lower.Contains);
    }
}
