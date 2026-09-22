using System.Security.Claims;

namespace PhysioTrac.Infrastructure.Identity;

/// <summary>Scoped holder for the authenticated principal in hosts where
/// <see cref="Microsoft.AspNetCore.Http.IHttpContextAccessor"/> isn't
/// reliable — Blazor Server's interactive circuit is a persistent
/// SignalR connection, not one HTTP request per render, so
/// `IHttpContextAccessor.HttpContext` is only guaranteed non-null during
/// the very first (prerender) request and goes null on later interactions.
///
/// A root Blazor component sets <see cref="Principal"/> once, from the
/// cascading `AuthenticationState`, when its circuit starts; the DI scope
/// (one per circuit in Blazor Server) then keeps it valid for the whole
/// session. The JSON API host never sets this, so <see cref="ClaimsCurrentUser"/>
/// falls back to <c>IHttpContextAccessor</c> there, unchanged.</summary>
public class CurrentUserAccessor
{
    public ClaimsPrincipal? Principal { get; set; }
}
