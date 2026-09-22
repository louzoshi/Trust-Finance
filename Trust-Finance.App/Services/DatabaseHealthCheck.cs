using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using TrustFinance.Data;

namespace TrustFinance.App.Services;

/// <summary>
/// What <c>/health</c> answers. It reads one row out of the database rather than asking
/// whether a connection can be opened: SQLite opens a connection to a file that has no
/// tables in it just as happily as to a good one, so "can connect" would report a database
/// that failed its migrations as healthy.
/// </summary>
public sealed class DatabaseHealthCheck(IDbContextFactory<TrustFinanceDbContext> factory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var db = await factory.CreateDbContextAsync(cancellationToken);
            await db.Users.AsNoTracking().AnyAsync(cancellationToken);
            return HealthCheckResult.Healthy("Banco de dados acessível");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Banco de dados inacessível", exception);
        }
    }
}
