using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TrustFinance.Data;

/// <summary>
/// Lets <c>dotnet ef migrations add</c> run against this project alone. The connection
/// string is never opened for a migration scaffold; it only has to be well-formed.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<TrustFinanceDbContext>
{
    public TrustFinanceDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TrustFinanceDbContext>()
            .UseSqlite(DatabaseLocation.ConnectionString("design-time.db"))
            .Options;
        return new TrustFinanceDbContext(options);
    }
}
