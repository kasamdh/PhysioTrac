using PhysioTrac.Domain.Common;

namespace PhysioTrac.Domain.Entities;

/// <summary>One entry in an organization's configurable insurance-payer
/// directory. Deliberately data-driven (timely filing days, authorization
/// requirement, free-text rules notes) rather than hard-coding any payer's
/// rules — billing staff maintain this directory themselves.</summary>
public class Payer : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? PayerId { get; set; }
    public string? ElectronicPayerId { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;
    public int TimelyFilingDays { get; set; } = 90;
    public bool AuthorizationRequired { get; set; }
    public string? AuthorizationNotes { get; set; }
    public string? Notes { get; set; }
    public Guid? CreatedById { get; set; }
}
