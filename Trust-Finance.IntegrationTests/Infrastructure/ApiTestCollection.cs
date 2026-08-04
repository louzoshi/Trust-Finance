namespace Trust_Finance.IntegrationTests.Infrastructure;

/// <summary>
/// Starting SQL Server costs several seconds, so every test class shares a single
/// container through this collection. xUnit runs classes in a collection sequentially,
/// which keeps the per-test database reset safe.
/// </summary>
[CollectionDefinition(Name)]
public class ApiTestCollection : ICollectionFixture<TrustFinanceApiFactory>
{
    public const string Name = "Trust Finance API";
}
