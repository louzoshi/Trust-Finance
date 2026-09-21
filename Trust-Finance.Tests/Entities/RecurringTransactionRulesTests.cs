using TrustFinance.Domain.Entities;

namespace TrustFinance.Tests.Entities;

public class RecurringTransactionRulesTests
{
    private const int Someone = 1;
    private const int Wallet = 9;
    private const int Category = 7;

    private static RecurringTransaction Monthly(DateOnly start, DateOnly? end = null, decimal amount = 1500m)
        => new("Aluguel", amount, TransactionType.Expense, Category, Wallet, Frequency.Monthly, start, end, Someone);

    [Fact]
    public void A_Good_Recurrence_Should_Be_Valid_And_Due_On_Its_Start_Date()
    {
        var rent = Monthly(new DateOnly(2026, 9, 10));

        rent.IsValid.Should().BeTrue();
        rent.NextDate.Should().Be(new DateOnly(2026, 9, 10));
        rent.IsFinished.Should().BeFalse();
    }

    [Theory]
    [InlineData("x", 100, "Description")]
    [InlineData("Aluguel", 0, "Amount")]
    [InlineData("Aluguel", -1, "Amount")]
    public void A_Bad_Recurrence_Should_Say_Which_Field(string description, decimal amount, string key)
    {
        var r = new RecurringTransaction(description, amount, TransactionType.Expense, Category, Wallet,
            Frequency.Monthly, new DateOnly(2026, 9, 1), null, Someone);

        r.IsInvalid.Should().BeTrue();
        r.Notifications.Should().Contain(n => n.Key == key);
    }

    [Fact]
    public void An_End_Before_The_Start_Should_Be_Rejected()
    {
        var r = Monthly(new DateOnly(2026, 9, 10), end: new DateOnly(2026, 9, 9));

        r.Notifications.Should().Contain(n => n.Key == "EndDate");
    }

    [Fact]
    public void PostDue_Should_Post_Nothing_Before_The_Start()
    {
        var rent = Monthly(new DateOnly(2026, 10, 10));

        rent.PostDue(new DateOnly(2026, 9, 20)).Should().BeEmpty();
        rent.NextDate.Should().Be(new DateOnly(2026, 10, 10));
    }

    [Fact]
    public void PostDue_Should_Post_Every_Occurrence_Up_To_Today_And_Advance()
    {
        var rent = Monthly(new DateOnly(2026, 6, 10));

        var posted = rent.PostDue(new DateOnly(2026, 9, 20));

        posted.Select(t => t.Date).Should().Equal(
            new DateOnly(2026, 6, 10), new DateOnly(2026, 7, 10), new DateOnly(2026, 8, 10), new DateOnly(2026, 9, 10));
        posted.Should().OnlyContain(t => t.IsValid && t.Description == "Aluguel" && t.Amount == 1500m
            && t.Type == TransactionType.Expense && t.CategoryId == Category && t.UserId == Someone);
        rent.NextDate.Should().Be(new DateOnly(2026, 10, 10));
    }

    [Fact]
    public void PostDue_Should_Be_Idempotent_Within_A_Day()
    {
        var rent = Monthly(new DateOnly(2026, 9, 10));
        var today = new DateOnly(2026, 9, 20);

        rent.PostDue(today).Should().HaveCount(1);
        rent.PostDue(today).Should().BeEmpty();
    }

    [Fact]
    public void PostDue_Should_Stop_At_The_End_Date()
    {
        var rent = Monthly(new DateOnly(2026, 6, 10), end: new DateOnly(2026, 7, 31));

        var posted = rent.PostDue(new DateOnly(2026, 9, 20));

        posted.Select(t => t.Date).Should().Equal(new DateOnly(2026, 6, 10), new DateOnly(2026, 7, 10));
        rent.IsFinished.Should().BeTrue();
    }

    [Fact]
    public void Correcting_The_Amount_Should_Not_Repost_What_Was_Already_Posted()
    {
        var rent = Monthly(new DateOnly(2026, 6, 10));
        rent.PostDue(new DateOnly(2026, 9, 20));

        rent.CorrectTo(Monthly(new DateOnly(2026, 6, 10), amount: 1600m));

        rent.Amount.Should().Be(1600m);
        rent.NextDate.Should().Be(new DateOnly(2026, 10, 10));
        rent.PostDue(new DateOnly(2026, 9, 20)).Should().BeEmpty();
    }

    [Fact]
    public void Correcting_The_Schedule_Should_Move_The_Next_Date_Forward_Only()
    {
        var rent = Monthly(new DateOnly(2026, 6, 10));
        rent.PostDue(new DateOnly(2026, 9, 20));

        // Moved to the 25th: the next one is 25 September, which has not been posted.
        rent.CorrectTo(Monthly(new DateOnly(2026, 6, 25)));
        rent.NextDate.Should().Be(new DateOnly(2026, 10, 25),
            "the floor is the old next date (10 Oct); the first 25th on or after it is 25 Oct");

        // Moved earlier in the calendar: nothing is back-filled.
        rent.CorrectTo(Monthly(new DateOnly(2026, 1, 5)));
        rent.NextDate.Should().Be(new DateOnly(2026, 11, 5));
    }

    [Fact]
    public void Correcting_With_Bad_Values_Should_Keep_The_Old_Ones()
    {
        var rent = Monthly(new DateOnly(2026, 9, 10));

        rent.CorrectTo(Monthly(new DateOnly(2026, 9, 10), amount: 0m));

        rent.IsInvalid.Should().BeTrue();
        rent.Amount.Should().Be(1500m);
    }

    [Fact]
    public void A_Posted_Transaction_Should_Trace_Back_To_Its_Recurrence()
    {
        var rent = Monthly(new DateOnly(2026, 9, 10));

        var posted = rent.PostDue(new DateOnly(2026, 9, 10)).Single();

        posted.IsRecurring.Should().BeTrue();
        posted.RecurringTransactionId.Should().Be(rent.Id);
    }
}
