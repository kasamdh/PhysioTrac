using System.Text.Json;
using PhysioTrac.Domain.Common;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Domain.Entities;

/// <summary>Billing draft that stores service codes for cash-pay billing,
/// never payment-card data.</summary>
public class Superbill : BaseEntity
{
    public Guid PatientId { get; set; }
    public Patient? Patient { get; set; }

    /// <summary>Rendering clinician. Stored by id only.</summary>
    public Guid ClinicianId { get; set; }

    public DateOnly ServiceDate { get; set; }

    /// <summary>JSON array of service codes.</summary>
    public string CodesJson { get; set; } = "[]";

    public decimal Amount { get; set; }
    public SuperbillStatus Status { get; set; } = SuperbillStatus.Draft;
    public string? PaymentProcessorReference { get; set; }

    public ICollection<Charge> Charges { get; set; } = new List<Charge>();
    public ICollection<PaymentRecord> Payments { get; set; } = new List<PaymentRecord>();

    /// <summary>Deserialized view of <see cref="CodesJson"/>, matching
    /// <see cref="Claim.DiagnosisCodeList"/>'s precedent for a JSON-blob list field.</summary>
    public IReadOnlyList<string> Codes
    {
        get => JsonSerializer.Deserialize<List<string>>(CodesJson) ?? new List<string>();
        set => CodesJson = JsonSerializer.Serialize(value);
    }
}
