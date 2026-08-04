using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Respawn;
using Respawn.Graph;
using TF.Data;
using Testcontainers.MsSql;

namespace Trust_Finance.IntegrationTests.Infrastructure;

/// <summary>
/// Boots the real API (the same <c>Program.cs</c> that runs in production) against a
/// throwaway SQL Server started by Testcontainers. Nothing is mocked or substituted:
/// the tests exercise the real middleware pipeline, the real DI graph, the real EF Core
/// SQL Server provider and the real migrations.
/// </summary>
public class TrustFinanceApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>Signing key handed to the API under test; also used to forge tokens.</summary>
    public const string JwtSigningKey = "trust-finance-integration-tests-signing-key";

    private const string TestDatabase = "TrustFinanceIntegrationTests";

    private readonly MsSqlContainer _sqlServer =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    private SqlConnection _respawnConnection = null!;
    private Respawner _respawner = null!;

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await _sqlServer.StartAsync();

        // Point the API at a dedicated database instead of writing into `master`.
        ConnectionString = new SqlConnectionStringBuilder(_sqlServer.GetConnectionString())
        {
            InitialCatalog = TestDatabase
        }.ConnectionString;

        // Program.cs reads the connection string and the JWT key from configuration while
        // the WebApplicationBuilder is still being assembled -- earlier than any
        // ConfigureAppConfiguration hook a WebApplicationFactory can register. Environment
        // variables are the one override that is guaranteed to be in place by then.
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", ConnectionString);
        Environment.SetEnvironmentVariable("JwtKey", JwtSigningKey);

        await ApplyMigrationsAsync();

        _respawnConnection = new SqlConnection(ConnectionString);
        await _respawnConnection.OpenAsync();

        _respawner = await Respawner.CreateAsync(_respawnConnection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.SqlServer,
            WithReseed = true,
            TablesToIgnore = new Table[] { new("__EFMigrationsHistory") }
        });
    }

    /// <summary>
    /// Wipes every table (and resets identity seeds) so each test starts from an empty
    /// database while still sharing one container across the whole run.
    /// </summary>
    public Task ResetDatabaseAsync() => _respawner.ResetAsync(_respawnConnection);

    /// <summary>Direct database access, for asserting on what was actually persisted.</summary>
    public TFDataContext CreateDbContext()
        => new(new DbContextOptionsBuilder<TFDataContext>()
            .UseSqlServer(ConnectionString)
            .Options);

    private async Task ApplyMigrationsAsync()
    {
        await using var context = new TFDataContext(
            new DbContextOptionsBuilder<TFDataContext>()
                .UseSqlServer(ConnectionString, sql => sql.EnableRetryOnFailure())
                .Options);

        await context.Database.MigrateAsync();
    }

    // Explicit implementation: WebApplicationFactory already exposes a ValueTask-returning
    // DisposeAsync, and xUnit's IAsyncLifetime wants a Task-returning one.
    async Task IAsyncLifetime.DisposeAsync()
    {
        if (_respawnConnection is not null)
            await _respawnConnection.DisposeAsync();

        await _sqlServer.DisposeAsync();
        await base.DisposeAsync();
    }
}
