namespace PhysioTrac.Application.Common;

/// <summary>Thrown when an authenticated actor is denied by a tenant or role
/// check. Maps to HTTP 403 at the API boundary.</summary>
public class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message) { }
}

/// <summary>Maps to HTTP 404 at the API boundary.</summary>
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}
