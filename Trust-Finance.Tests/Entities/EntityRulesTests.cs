using TrustFinance.Domain.Entities;

namespace TrustFinance.Tests.Entities;

/// <summary>
/// The point of the rich model: an entity cannot be constructed into a state the rest of
/// the app would have to defend against. These are the rules that used to live in the
/// service layer, where a second caller could bypass them.
/// </summary>
public class TradeRulesTests
{
    private const int Someone = 1;
    const int Wallet = 9;
    private static readonly DateOnly Date = new(2026, 9, 10);

    private static Trade Valid() => new("petr4", AssetClass.Stock, TradeSide.Buy, 100, 30m, 4.90m, Date, Someone);

    [Fact]
    public void A_Good_Trade_Should_Be_Valid_And_Normalized()
    {
        var trade = Valid();

        trade.IsValid.Should().BeTrue();
        trade.Ticker.Should().Be("PETR4", "the ticker value object upper-cases it");
        trade.Gross.Should().Be(3000m);
        trade.NetCashFlow.Should().Be(3004.90m, "a buy costs the fees too");
    }

    [Fact]
    public void A_Sell_Should_Net_The_Fees_Out_Of_The_Proceeds()
    {
        var trade = new Trade("PETR4", AssetClass.Stock, TradeSide.Sell, 100, 30m, 4.90m, Date, Someone);

        trade.NetCashFlow.Should().Be(2995.10m);
    }

    [Theory]
    [InlineData(0, 30, 0, "Quantity")]
    [InlineData(-5, 30, 0, "Quantity")]
    [InlineData(100, 0, 0, "UnitPrice")]
    [InlineData(100, -1, 0, "UnitPrice")]
    [InlineData(100, 30, -1, "Fees")]
    public void Should_Refuse_Numbers_That_Break_The_Portfolio_Maths(
        decimal quantity, decimal price, decimal fees, string key)
    {
        var trade = new Trade("PETR4", AssetClass.Stock, TradeSide.Buy, quantity, price, fees, Date, Someone);

        trade.IsInvalid.Should().BeTrue();
        trade.Notifications.Should().Contain(n => n.Key == key);
    }

    [Fact]
    public void Should_Collect_Every_Problem_At_Once_Not_Just_The_First()
    {
        var trade = new Trade("", AssetClass.Stock, TradeSide.Buy, -1, -1, -1, default, 0);

        // An exception would have reported one. The user gets the whole list.
        trade.Notifications.Should().HaveCountGreaterThan(4);
    }

    [Fact]
    public void A_Correction_Should_Go_Through_The_Same_Rules()
    {
        var trade = Valid();
        var bad = new Trade("PETR4", AssetClass.Stock, TradeSide.Buy, -99, 30m, 0, Date, Someone);

        trade.CorrectTo(bad);

        trade.IsInvalid.Should().BeTrue();
        trade.Quantity.Should().Be(100, "an invalid correction must not be applied");
    }

    [Fact]
    public void A_Valid_Correction_Should_Be_Applied()
    {
        var trade = Valid();
        var better = new Trade("vale3", AssetClass.Stock, TradeSide.Buy, 50, 61m, 0, Date, Someone);

        trade.CorrectTo(better);

        trade.IsValid.Should().BeTrue();
        trade.Ticker.Should().Be("VALE3");
        trade.Quantity.Should().Be(50);
    }
}

public class TransactionRulesTests
{
    private const int Someone = 1;
    private const int Wallet = 9;
    private static readonly DateOnly Date = new(2026, 9, 10);

    [Fact]
    public void Direction_Should_Come_From_The_Type_Not_The_Sign()
    {
        var expense = new Transaction("Mercado", 100m, Date, TransactionType.Expense, 1, Wallet, Someone);
        var income = new Transaction("Salário", 100m, Date, TransactionType.Income, 1, Wallet, Someone);

        expense.SignedAmount.Should().Be(-100m);
        income.SignedAmount.Should().Be(100m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void Should_Refuse_A_Non_Positive_Amount(decimal amount)
    {
        var transaction = new Transaction("Mercado", amount, Date, TransactionType.Expense, 1, Wallet, Someone);

        transaction.IsInvalid.Should().BeTrue();
        transaction.Notifications.Should().Contain(n => n.Key == "Amount");
    }

    [Fact]
    public void Should_Trim_The_Description()
    {
        new Transaction("  Mercado  ", 10m, Date, TransactionType.Expense, 1, Wallet, Someone)
            .Description.Should().Be("Mercado");
    }

    [Fact]
    public void Should_Refuse_An_Unset_Category()
    {
        new Transaction("Mercado", 10m, Date, TransactionType.Expense, 0, Wallet, Someone)
            .Notifications.Should().Contain(n => n.Key == "CategoryId");
    }
}

public class CategoryRulesTests
{
    [Fact]
    public void Slug_Should_Be_Derived_And_Not_Settable()
    {
        var category = new Category("  Mercado & Padaria ", 1);

        category.IsValid.Should().BeTrue();
        category.Name.Should().Be("Mercado & Padaria");
        category.Slug.Should().Be("mercado-padaria");
    }

    [Fact]
    public void Renaming_Should_Move_The_Slug_With_It()
    {
        var category = new Category("Mercado", 1);

        category.Rename("Educação");

        category.Slug.Should().Be("educacao");
    }

    [Theory]
    [InlineData("A")]
    [InlineData("")]
    public void Should_Refuse_A_Name_That_Is_Too_Short(string name) =>
        new Category(name, 1).IsInvalid.Should().BeTrue();
}

public class PriceAlertRulesTests
{
    private static PriceAlert Armed(AlertDirection direction, decimal target) =>
        new("PETR4", AssetClass.Stock, direction, target, 1);

    [Theory]
    [InlineData(AlertDirection.Above, 38, 38, true)]
    [InlineData(AlertDirection.Above, 38, 40, true)]
    [InlineData(AlertDirection.Above, 38, 37, false)]
    [InlineData(AlertDirection.Below, 30, 30, true)]
    [InlineData(AlertDirection.Below, 30, 25, true)]
    [InlineData(AlertDirection.Below, 30, 31, false)]
    public void Crossing_Should_Include_The_Target_Itself(
        AlertDirection direction, decimal target, decimal price, bool expected) =>
        Armed(direction, target).WouldTriggerAt(price).Should().Be(expected);

    [Fact]
    public void Should_Fire_Once_And_Keep_The_Price_That_Fired_It()
    {
        var alert = Armed(AlertDirection.Above, 38m);
        var at = new DateTime(2026, 9, 21, 10, 0, 0, DateTimeKind.Utc);

        alert.Trigger(40m, at).Should().BeTrue();
        alert.TriggeredPrice.Should().Be(40m);
        alert.IsArmed.Should().BeFalse();

        // A second sweep must not resurrect a notification the user already dismissed.
        alert.Trigger(50m, at.AddHours(1)).Should().BeFalse();
        alert.TriggeredPrice.Should().Be(40m);
    }

    [Fact]
    public void Should_Refuse_A_Non_Positive_Target() =>
        Armed(AlertDirection.Above, 0m).IsInvalid.Should().BeTrue();
}
