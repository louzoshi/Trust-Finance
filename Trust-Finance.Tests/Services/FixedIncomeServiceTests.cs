using Microsoft.Extensions.Caching.Memory;
using TrustFinance.App.Services;
using TrustFinance.App.Services.Market;
using TrustFinance.Domain.Entities;
using TrustFinance.Tests.Fixtures;

namespace TrustFinance.Tests.Services;

public class FixedIncomeServiceTests : IDisposable
{
    private readonly SqliteDatabase _db = new();
    private readonly FixedClock _clock = new(new DateOnly(2026, 9, 21));
    private readonly FixedIncomeService _service;

    public FixedIncomeServiceTests()
    {
        // No HTTP: the BCB client falls back to a flat series, which is what a machine
        // with no internet gets and is enough to exercise the accrual path.
        var cache = new MemoryCache(new MemoryCacheOptions());
        var series = new BcbSeries(new OfflineHttpClientFactory(), cache, new NullLogger<BcbSeries>());
        var settings = new SettingsService(_db);
        var market = new MarketData(new OfflineHttpClientFactory(), settings, cache, _clock, new NullLoggerFactory());

        _service = new FixedIncomeService(_db, series, market, _clock);
    }

    public void Dispose() => _db.Dispose();

    private static FixedIncomeInvestment Cdb(int userId, decimal principal = 10_000m, string issuer = "Banco Exemplo",
        FixedIncomeKind kind = FixedIncomeKind.Cdb, IndexKind index = IndexKind.Fixed, decimal rate = 12m)
        => new(issuer, kind, index, rate, principal, new DateOnly(2025, 9, 22), new DateOnly(2028, 9, 22), userId);

    [Fact]
    public async Task A_Paper_Should_Round_Trip_Its_Rate_And_Principal()
    {
        var created = (await _service.CreateAsync(Cdb(_db.Ada, 12_345.67m))).Value!;

        var stored = (await _service.GetAllAsync(_db.Ada)).Single();
        stored.Id.Should().Be(created.Id);
        stored.Principal.Should().Be(12_345.67m);
        stored.Rate.Should().Be(12m);
        (await _service.GetAllAsync(_db.Bob)).Should().BeEmpty();
    }

    [Fact]
    public async Task A_Percentage_Of_The_Cdi_Should_Keep_Its_Decimals()
    {
        await _service.CreateAsync(Cdb(_db.Ada, index: IndexKind.PercentOfCdi, rate: 102.5m));

        (await _service.GetAllAsync(_db.Ada)).Single().Rate.Should().Be(102.5m);
    }

    [Fact]
    public async Task An_Invalid_Paper_Should_Be_Rejected_With_Its_Messages()
    {
        var result = await _service.CreateAsync(new FixedIncomeInvestment(
            "x", FixedIncomeKind.Cdb, IndexKind.Fixed, -1m, 0m,
            new DateOnly(2026, 9, 1), new DateOnly(2026, 1, 1), _db.Ada));

        result.Failed.Should().BeTrue();
        result.Notifications.Select(n => n.Key).Should().Contain(["Issuer", "Rate", "Principal", "MaturityDate"]);
    }

    [Fact]
    public async Task Update_And_Delete_Should_Not_Reach_Another_Users_Paper()
    {
        var created = (await _service.CreateAsync(Cdb(_db.Ada))).Value!;

        (await _service.UpdateAsync(created.Id, Cdb(_db.Bob, 99m))).Failed.Should().BeTrue();
        (await _service.DeleteAsync(created.Id, _db.Bob)).Failed.Should().BeTrue();
        (await _service.RedeemAsync(created.Id, new DateOnly(2026, 5, 1), _db.Bob)).Failed.Should().BeTrue();

        (await _service.GetAllAsync(_db.Ada)).Single().Principal.Should().Be(10_000m);
    }

    [Fact]
    public async Task Redeeming_Should_Stop_The_Accrual_And_Leave_The_Book()
    {
        var created = (await _service.CreateAsync(Cdb(_db.Ada))).Value!;

        var before = (await _service.GetBookAsync(_db.Ada)).OnCurve;
        (await _service.RedeemAsync(created.Id, new DateOnly(2026, 3, 20), _db.Ada)).Success.Should().BeTrue();
        var after = await _service.GetBookAsync(_db.Ada);

        after.OnCurve.Should().Be(0m, "a redeemed paper is no longer held");
        after.Papers.Should().ContainSingle().Which.OnCurve.Should().BeLessThan(before, "it stopped earning in March");
    }

    [Fact]
    public async Task A_Redemption_Before_The_Purchase_Should_Be_Refused()
    {
        var created = (await _service.CreateAsync(Cdb(_db.Ada))).Value!;

        var result = await _service.RedeemAsync(created.Id, new DateOnly(2020, 1, 1), _db.Ada);

        result.Failed.Should().BeTrue();
        (await _service.GetByIdAsync(created.Id, _db.Ada))!.IsOpen.Should().BeTrue();
    }

    [Fact]
    public async Task The_Book_Should_Total_What_Is_Open_And_Flag_The_Fgc()
    {
        await _service.CreateAsync(Cdb(_db.Ada, 200_000m, "Banco A"));
        await _service.CreateAsync(Cdb(_db.Ada, 100_000m, "Banco A"));
        await _service.CreateAsync(Cdb(_db.Ada, 50_000m, "Tesouro Nacional", FixedIncomeKind.TesouroSelic, IndexKind.SelicPlus, 0m));

        var book = await _service.GetBookAsync(_db.Ada);

        book.Invested.Should().Be(350_000m);
        book.OnCurve.Should().BeGreaterThan(book.Invested);
        book.NetValue.Should().BeLessThan(book.OnCurve, "IR and custody come off");

        book.Fgc.Should().ContainSingle(e => e.Issuer == "Banco A");
        book.Fgc.Single().Uncovered.Should().BeGreaterThan(0m, "R$ 300.000 at one bank passes the R$ 250.000 limit");
        book.UncoveredByFgc.Should().BeGreaterThan(0m);
    }

    [Fact]
    public async Task An_Empty_Book_Should_Say_So_Without_Asking_The_Central_Bank()
    {
        var book = await _service.GetBookAsync(_db.Ada);

        book.IsEmpty.Should().BeTrue();
        book.OnCurve.Should().Be(0m);
        book.Papers.Should().BeEmpty();
    }

    [Fact]
    public async Task Papers_Should_Be_Listed_By_Maturity_With_Redeemed_Ones_Last()
    {
        var soon = (await _service.CreateAsync(new FixedIncomeInvestment(
            "Banco A", FixedIncomeKind.Cdb, IndexKind.Fixed, 12m, 1000m,
            new DateOnly(2025, 1, 10), new DateOnly(2027, 1, 10), _db.Ada))).Value!;
        await _service.CreateAsync(new FixedIncomeInvestment(
            "Banco B", FixedIncomeKind.Cdb, IndexKind.Fixed, 12m, 1000m,
            new DateOnly(2025, 1, 10), new DateOnly(2026, 12, 10), _db.Ada));
        await _service.RedeemAsync(soon.Id, new DateOnly(2026, 1, 10), _db.Ada);

        var papers = await _service.GetAllAsync(_db.Ada);

        papers.Select(p => p.Issuer).Should().Equal("Banco B", "Banco A");
    }
}
