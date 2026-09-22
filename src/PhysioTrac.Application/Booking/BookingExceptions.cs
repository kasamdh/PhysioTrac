namespace PhysioTrac.Application.Booking;

/// <summary>Direct port of `care/booking.py`'s `BookingError` hierarchy —
/// `Code` maps to a stable frontend-facing string, `Status` to the HTTP
/// status the API layer returns.</summary>
public class BookingException : Exception
{
    public string Code { get; }
    public int Status { get; }
    public string? Field { get; }

    public BookingException(string message, string code = "BOOKING_ERROR", int status = 400, string? field = null) : base(message)
    {
        Code = code;
        Status = status;
        Field = field;
    }
}

public class BookingNotFoundException : BookingException
{
    public BookingNotFoundException(string message) : base(message, "NOT_FOUND", 404) { }
}

public class BookingValidationException : BookingException
{
    public BookingValidationException(string message, string? field = null) : base(message, "VALIDATION_FAILED", 422, field) { }
}

public class SlotNoLongerAvailableException : BookingException
{
    public SlotNoLongerAvailableException()
        : base("This appointment time was just booked. Please select another available time.", "SLOT_NO_LONGER_AVAILABLE", 409) { }
}

public class ChangeCutoffException : BookingException
{
    public ChangeCutoffException()
        : base("This appointment is too close to reschedule or cancel online. Please call the clinic.", "CHANGE_CUTOFF", 409) { }
}

public class RateLimitedException : BookingException
{
    public RateLimitedException() : base("Too many requests. Please try again shortly.", "RATE_LIMITED", 429) { }
}
