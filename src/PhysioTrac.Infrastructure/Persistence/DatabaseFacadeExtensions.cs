using Microsoft.EntityFrameworkCore.Infrastructure;

namespace PhysioTrac.Infrastructure.Persistence;

public static class DatabaseFacadeExtensions
{
    /// <summary>True only for the real SQL Server provider — unlike
    /// <c>IsRelational()</c>, which is also true for SQLite (used for local
    /// dev/tests). Several services send raw T-SQL (<c>WITH (UPDLOCK,
    /// ROWLOCK)</c>, etc.) that only SQL Server understands; guard those
    /// calls with this, not <c>IsRelational()</c>. Named distinctly from EF
    /// Core's own <c>IsSqlServer()</c> (in the SqlServer provider package)
    /// to avoid an ambiguous-call error when both are in scope.</summary>
    public static bool IsRealSqlServer(this DatabaseFacade database) =>
        database.ProviderName == "Microsoft.EntityFrameworkCore.SqlServer";
}
