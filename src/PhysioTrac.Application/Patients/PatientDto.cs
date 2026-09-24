using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Application.Patients;

public record PatientDto(
    Guid Id,
    string MedicalRecordNumber,
    string FirstName,
    string LastName,
    string FullName,
    DateOnly DateOfBirth,
    int Age,
    string? Phone,
    string? Email,
    Guid? AssignedTherapistId,
    PatientStatus Status,
    Guid? PrimaryLocationId);

/// <summary>The full chart-header view -- everything PatientDto has, plus
/// the fields only worth fetching when a specific chart is actually opened
/// (not for a list row). Allergies/medications/diagnoses are each fetched
/// separately (their own list endpoints) so this stays a fast single-row
/// read; a client assembles the full chart from this plus those.</summary>
public record PatientDetailDto(
    Guid Id,
    string MedicalRecordNumber,
    string FirstName,
    string LastName,
    string FullName,
    DateOnly DateOfBirth,
    int Age,
    string? Phone,
    string? Email,
    string? Address,
    string? EmergencyContact,
    string? PreferredLanguage,
    string? Diagnoses,
    string? Precautions,
    Guid? AssignedTherapistId,
    Guid? PrimaryLocationId,
    Guid? PrimaryCareProviderId,
    Guid? ReferringProviderId,
    PatientStatus Status);

public record CreatePatientRequest(
    string FirstName,
    string LastName,
    DateOnly DateOfBirth,
    string? Phone,
    string? Email,
    string? Address,
    string? EmergencyContact,
    string? PreferredLanguage,
    Guid? AssignedTherapistId,
    Guid? PrimaryLocationId,
    Guid? PrimaryCareProviderId,
    Guid? ReferringProviderId);

public record UpdatePatientRequest(
    string FirstName,
    string LastName,
    string? Phone,
    string? Email,
    string? Address,
    string? EmergencyContact,
    string? PreferredLanguage,
    Guid? AssignedTherapistId,
    Guid? PrimaryLocationId,
    Guid? PrimaryCareProviderId,
    Guid? ReferringProviderId,
    PatientStatus Status);

/// <summary>Query parameters for GET /api/v1/patients -- all optional.
/// SortBy is a small fixed vocabulary ("lastName" default, "createdAt",
/// "dateOfBirth"), not an arbitrary column name, to avoid ever building a
/// dynamic OrderBy off unsanitized client input.</summary>
public record PatientSearchQuery(
    string? Search, PatientStatus? Status, string? SortBy, bool Descending, int Page, int PageSize);

public record PagedPatientsDto(IReadOnlyList<PatientDto> Items, int Total, int Page, int PageSize);
