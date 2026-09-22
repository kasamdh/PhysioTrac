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
    PatientStatus Status);

public record CreatePatientRequest(
    string FirstName,
    string LastName,
    DateOnly DateOfBirth,
    string? Phone,
    string? Email,
    Guid? AssignedTherapistId);

public record UpdatePatientRequest(
    string FirstName,
    string LastName,
    string? Phone,
    string? Email,
    Guid? AssignedTherapistId,
    PatientStatus Status);
