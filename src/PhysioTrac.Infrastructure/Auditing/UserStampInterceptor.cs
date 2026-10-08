using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PhysioTrac.Application.Auth;
using PhysioTrac.Domain.Common;

namespace PhysioTrac.Infrastructure.Auditing;

/// <summary>Fills CreatedById / UpdatedById on <see cref="IUserStamped"/>
/// rows from the signed-in user, and keeps UpdatedAt current (UTC). System
/// writes (seeding, no signed-in user) leave the user ids as they are.</summary>
public class UserStampInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUser _currentUser;

    public UserStampInterceptor(ICurrentUser currentUser)
    {
        _currentUser = currentUser;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Stamp(DbContext? context)
    {
        if (context is null) return;
        var userId = _currentUser.IsAuthenticated && _currentUser.UserId != Guid.Empty ? _currentUser.UserId : (Guid?)null;
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in context.ChangeTracker.Entries<IUserStamped>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedById ??= userId;
                entry.Entity.UpdatedById ??= userId;
            }
            else if (entry.State == EntityState.Modified)
            {
                if (userId is not null) entry.Entity.UpdatedById = userId;
                if (entry.Entity is BaseEntity b) b.UpdatedAt = now;
            }
        }
    }
}
