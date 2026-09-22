using System.Text.Json;
using PhysioTrac.Domain.Common;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Domain.Entities;

/// <summary>An insurance claim — one or more Charge line items billed
/// together against one patient insurance policy. `DiagnosisCodeListJson`
/// is an ordered snapshot (CMS-1500 box 21, lettered A-L) built once at
/// claim creation from the union of its charges' diagnosis codes. Cash-pay
/// billing uses <see cref="Superbill"/>, not Claim — a claim always
/// requires insurance.</summary>
public class Claim : BaseEntity
{
    /// <summary>Statuses representing a claim actively awaiting payer
    /// action — once a claim leaves Draft/Ready/ValidationError, charges are locked in.</summary>
    public static readonly IReadOnlySet<ClaimStatus> OpenStatuses = new HashSet<ClaimStatus>
    {
        ClaimStatus.Submitted, ClaimStatus.Accepted, ClaimStatus.Processing, ClaimStatus.PartialPayment, ClaimStatus.Appealed,
    };

    public static readonly IReadOnlySet<ClaimStatus> TerminalStatuses = new HashSet<ClaimStatus> { ClaimStatus.Paid, ClaimStatus.Closed };

    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }

    public Guid PatientId { get; set; }
    public Patient? Patient { get; set; }

    public Guid PatientInsuranceId { get; set; }
    public PatientInsurance? PatientInsurance { get; set; }

    public Guid PayerId { get; set; }
    public Payer? Payer { get; set; }

    /// <summary>Ordered list of ICD-10 codes, JSON-serialized (at most 12).</summary>
    public string DiagnosisCodeListJson { get; set; } = "[]";

    public ClaimStatus Status { get; set; } = ClaimStatus.Draft;
    public string? ClearinghouseClaimId { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public Guid? CreatedById { get; set; }

    public ICollection<Charge> Charges { get; set; } = new List<Charge>();
    public ICollection<ClaimTransaction> Transactions { get; set; } = new List<ClaimTransaction>();
    public ICollection<ClaimDenial> Denials { get; set; } = new List<ClaimDenial>();

    public IReadOnlyList<string> DiagnosisCodeList
    {
        get => JsonSerializer.Deserialize<List<string>>(DiagnosisCodeListJson) ?? new List<string>();
        set => DiagnosisCodeListJson = JsonSerializer.Serialize(value);
    }

    /// <summary>CMS-1500 box 24E letters (A-L) for one charge line, derived
    /// from this claim's stored diagnosis code list — never stored per charge.</summary>
    public IReadOnlyList<string> DiagnosisPointersFor(Charge charge, IReadOnlySet<string> codesOnCharge)
    {
        var list = DiagnosisCodeList;
        var pointers = new List<string>();
        for (var index = 0; index < list.Count; index++)
        {
            if (codesOnCharge.Contains(list[index]))
            {
                pointers.Add(((char)('A' + index)).ToString());
            }
        }
        return pointers;
    }
}
