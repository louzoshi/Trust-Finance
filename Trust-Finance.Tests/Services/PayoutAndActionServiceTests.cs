using TrustFinance.App.Services;
using TrustFinance.Domain.Entities;
using TrustFinance.Tests.Fixtures;

namespace TrustFinance.Tests.Services;

public class PayoutAndActionServiceTests : IDisposable
{
    private readonly SqliteDatabase _db = new();
    private readonly PayoutService _payouts;
    private readonly CorporateActionService _actions;

    public PayoutAndActionServiceTests()
    {
        _payouts = new PayoutService(_db);
        _actions = new CorporateActionService(_db);
    }

    public void Dispose() => _db.Dispose();

    private static Payout Dividend(int userId, decimal gross = 100m)
        => new("PETR4", PayoutKind.Dividend, new DateOnly(2026, 9, 10), 100, gross, 0m, userId);

    private static CorporateAction Split(int userId)
        => new("MGLU3", CorporateActionKind.Split, new DateOnly(2026, 9, 10), 10m, null, userId);

    [Fact]
    public async Task A_Payout_Should_Keep_Its_Cents_And_Its_Owner()
    {
        var created = (await _payouts.CreateAsync(new Payout("ITSA4", PayoutKind.InterestOnEquity,
            new DateOnly(2026, 9, 1), 2500, 700.55m, 105.08m, _db.Ada))).Value!;

        var stored = (await _payouts.GetAllAsync(_db.Ada)).Single();
        stored.Id.Should().Be(created.Id);
        stored.GrossAmount.Should().Be(700.55m);
        stored.WithheldTax.Should().Be(105.08m);
        stored.NetAmount.Should().Be(595.47m);
        (await _payouts.GetAllAsync(_db.Bob)).Should().BeEmpty();
    }

    [Fact]
    public async Task Payout_Update_And_Delete_Should_Not_Reach_Another_User()
    {
        var created = (await _payouts.CreateAsync(Dividend(_db.Ada))).Value!;

        (await _payouts.UpdateAsync(created.Id, Dividend(_db.Bob, 999m), created.Version)).Failed.Should().BeTrue();
        (await _payouts.DeleteAsync(created.Id, _db.Bob)).Failed.Should().BeTrue();

        (await _payouts.GetAllAsync(_db.Ada)).Single().GrossAmount.Should().Be(100m);
    }

    [Fact]
    public async Task An_Invalid_Payout_Should_Be_Rejected_With_Its_Messages()
    {
        var result = await _payouts.CreateAsync(new Payout("PETR4", PayoutKind.Dividend, new DateOnly(2026, 9, 1), 0, 0m, 0m, _db.Ada));

        result.Failed.Should().BeTrue();
        result.Notifications.Select(n => n.Key).Should().Contain(["Quantity", "GrossAmount"]);
    }

    [Fact]
    public async Task A_Corporate_Action_Should_Round_Trip_Its_Factor()
    {
        var bonus = new CorporateAction("ITSA4", CorporateActionKind.Bonus, new DateOnly(2026, 4, 30), 0.05m, 7.123456m, _db.Ada);

        await _actions.CreateAsync(bonus);

        var stored = (await _actions.GetAllAsync(_db.Ada)).Single();
        stored.Factor.Should().Be(0.05m);
        stored.BonusUnitCost.Should().Be(7.123456m);
    }

    [Fact]
    public async Task Action_Update_And_Delete_Should_Not_Reach_Another_User()
    {
        var created = (await _actions.CreateAsync(Split(_db.Ada))).Value!;

        (await _actions.UpdateAsync(created.Id, Split(_db.Bob), created.Version)).Failed.Should().BeTrue();
        (await _actions.DeleteAsync(created.Id, _db.Bob)).Failed.Should().BeTrue();
        (await _actions.GetAllAsync(_db.Ada)).Should().ContainSingle();

        (await _actions.DeleteAsync(created.Id, _db.Ada)).Success.Should().BeTrue();
        (await _actions.GetAllAsync(_db.Ada)).Should().BeEmpty();
    }
}
