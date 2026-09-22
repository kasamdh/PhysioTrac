using PhysioTrac.Domain.Common;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Domain.Entities;

/// <summary>Org-configurable cash-pay price list — one row per CPT/service,
/// the same data-driven-not-hard-coded pattern as <see cref="Payer"/>.
///
/// `HomeVisitKind`/`IsHomeVisitTravelFee` let an org tag up to one row per
/// <see cref="AppointmentKind"/> plus at most one row as the org's optional
/// travel fee — mutually exclusive on one row. `DepositAmount` is only
/// meaningful on a home-visit-kind row.
///
/// Mobile Care's own billing module (which reads these tags) isn't ported —
/// these fields exist for future use and current validation parity, but
/// nothing reads them yet.</summary>
public class ServicePrice : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }

    public string CptCode { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsActive { get; set; } = true;
    public AppointmentKind? HomeVisitKind { get; set; }
    public bool IsHomeVisitTravelFee { get; set; }
    public decimal? DepositAmount { get; set; }
    public Guid? CreatedById { get; set; }
}
