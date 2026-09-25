using PhysioTrac.Api.Controllers;
using PhysioTrac.Application.Clinical;
using PhysioTrac.Domain.Enums;
using Xunit;

namespace PhysioTrac.Tests;

/// <summary>Phase 9's input-validation-review ask, made concrete: these ten
/// mutation DTOs had no FluentValidation coverage despite taking raw
/// money/numeric/free-text input straight from a request body. One
/// valid-passes/invalid-fails pair per validator proves each rule actually
/// fires -- these run through FluentValidation directly, not the API
/// pipeline, since FluentValidationActionFilter only executes inside a real
/// MVC action invocation.</summary>
public class RequestValidatorsTests
{
    [Fact]
    public void PatientPayment_NonPositiveAmount_FailsValidation()
    {
        var validator = new CreatePatientPaymentRequestValidator();
        Assert.False(validator.Validate(new CreatePatientPaymentRequest(0m, PatientPaymentStatus.Succeeded, null, null)).IsValid);
        Assert.True(validator.Validate(new CreatePatientPaymentRequest(25m, PatientPaymentStatus.Succeeded, null, null)).IsValid);
    }

    [Fact]
    public void Superbill_NegativeAmountOrMissingIds_FailsValidation()
    {
        var validator = new CreateSuperbillRequestValidator();
        Assert.False(validator.Validate(new CreateSuperbillRequest(Guid.Empty, Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow), null, 100m, null)).IsValid);
        Assert.False(validator.Validate(new CreateSuperbillRequest(Guid.NewGuid(), Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow), null, -1m, null)).IsValid);
        Assert.True(validator.Validate(new CreateSuperbillRequest(Guid.NewGuid(), Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow), null, 100m, null)).IsValid);
    }

    [Fact]
    public void GoalProgress_NegativeCurrentValue_FailsValidation()
    {
        var validator = new UpdateGoalProgressRequestValidator();
        Assert.False(validator.Validate(new UpdateGoalProgressRequest(-1m)).IsValid);
        Assert.True(validator.Validate(new UpdateGoalProgressRequest(5m)).IsValid);
    }

    [Fact]
    public void ServicePrice_MalformedCptCodeOrNegativePrice_FailsValidation()
    {
        var validator = new CreateServicePriceRequestValidator();
        Assert.False(validator.Validate(new CreateServicePriceRequest("not-a-cpt", "Label", 65m, null, null, false, null)).IsValid);
        Assert.False(validator.Validate(new CreateServicePriceRequest("97110", "Label", -1m, null, null, false, null)).IsValid);
        Assert.True(validator.Validate(new CreateServicePriceRequest("97110", "Label", 65m, null, null, false, null)).IsValid);
    }

    [Fact]
    public void AppointmentTypeBilling_MalformedCptCode_FailsValidation()
    {
        var validator = new UpdateAppointmentTypeBillingRequestValidator();
        Assert.False(validator.Validate(new UpdateAppointmentTypeBillingRequest("bad", 95m)).IsValid);
        Assert.True(validator.Validate(new UpdateAppointmentTypeBillingRequest("97110", 95m)).IsValid);
        Assert.True(validator.Validate(new UpdateAppointmentTypeBillingRequest(null, null)).IsValid);
    }

    [Fact]
    public void PatientAllergy_BlankAllergen_FailsValidation()
    {
        var validator = new CreatePatientAllergyRequestValidator();
        Assert.False(validator.Validate(new CreatePatientAllergyRequest("   ", null, AllergySeverity.Mild, null)).IsValid);
        Assert.True(validator.Validate(new CreatePatientAllergyRequest("Penicillin", "Hives", AllergySeverity.Mild, null)).IsValid);
    }

    [Fact]
    public void ReferringProvider_InvalidNpiOrEmail_FailsValidation()
    {
        var validator = new CreateReferringProviderRequestValidator();
        Assert.False(validator.Validate(new CreateReferringProviderRequest("Nadia", "Farouk", "123", null, null, null, null, null)).IsValid);
        Assert.False(validator.Validate(new CreateReferringProviderRequest("Nadia", "Farouk", null, null, null, null, "not-an-email", null)).IsValid);
        Assert.True(validator.Validate(new CreateReferringProviderRequest("Nadia", "Farouk", "1234567890", null, null, null, "n@example.com", null)).IsValid);
    }

    [Fact]
    public void Payer_BlankNameOrNonPositiveFilingDays_FailsValidation()
    {
        var validator = new CreatePayerRequestValidator();
        Assert.False(validator.Validate(new CreatePayerRequest("", null, null, null, false, null, null)).IsValid);
        Assert.False(validator.Validate(new CreatePayerRequest("Aetna", null, null, 0, false, null, null)).IsValid);
        Assert.True(validator.Validate(new CreatePayerRequest("Aetna", null, null, 90, false, null, null)).IsValid);
    }

    [Fact]
    public void Room_BlankName_FailsValidation()
    {
        var validator = new CreateRoomRequestValidator();
        Assert.False(validator.Validate(new CreateRoomRequest("   ")).IsValid);
        Assert.True(validator.Validate(new CreateRoomRequest("Treatment Room 1")).IsValid);
    }

    [Fact]
    public void CptCodeMapping_MalformedCptCode_FailsValidation()
    {
        var validator = new CreateCptCodeMappingRequestValidator();
        Assert.False(validator.Validate(new CreateCptCodeMappingRequest(InterventionCategory.TherapeuticExercise, "bad")).IsValid);
        Assert.True(validator.Validate(new CreateCptCodeMappingRequest(InterventionCategory.TherapeuticExercise, "97110")).IsValid);
    }
}
