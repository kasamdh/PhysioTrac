namespace PhysioTrac.Application.Booking;

public record PortalBookingRequest(Guid LocationId, Guid AppointmentTypeId, Guid ProviderId, DateTimeOffset StartDatetime, string? ReasonForVisit);

public record PortalRescheduleRequest(DateTimeOffset StartDatetime);

public record JoinWaitlistRequest(Guid? LocationId, Guid? AppointmentTypeId, Guid? ProviderId, DateOnly EarliestDate, DateOnly? LatestDate, string? Notes);
