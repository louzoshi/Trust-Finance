using TrustFinance.Domain.Entities;

namespace TrustFinance.Tests.Entities;

public class AccountRulesTests
{
    private const int Someone = 1;

    [Fact]
    public void A_Card_Needs_Its_Two_Days()
    {
        var card = new Account("Nubank", AccountKind.CreditCard, Someone);

        card.Notifications.Select(n => n.Key).Should().Contain(["ClosingDay", "DueDay"]);
    }

    [Fact]
    public void A_Checking_Account_Ignores_The_Card_Days()
    {
        var checking = new Account("Itaú", AccountKind.Checking, Someone, closingDay: 5, dueDay: 12);

        checking.IsValid.Should().BeTrue();
        checking.ClosingDay.Should().BeNull();
    }

    [Theory]
    [InlineData("2026-09-05", "2026-09-10")]  // before closing: this month's statement
    [InlineData("2026-09-10", "2026-09-10")]  // on the closing day: still this one
    [InlineData("2026-09-11", "2026-10-10")]  // the day after: next month's
    public void A_Purchase_Lands_On_The_Statement_Closing_On_Or_After_It(string bought, string closes)
    {
        var card = new Account("Nubank", AccountKind.CreditCard, Someone, closingDay: 10, dueDay: 17);

        card.StatementClosingFor(DateOnly.Parse(bought)).Should().Be(DateOnly.Parse(closes));
    }

    [Fact]
    public void The_Due_Date_Follows_The_Closing_Even_Across_A_Month_End()
    {
        var card = new Account("Nubank", AccountKind.CreditCard, Someone, closingDay: 28, dueDay: 5);

        card.DueDateFor(new DateOnly(2026, 9, 28)).Should().Be(new DateOnly(2026, 10, 5));
    }

    [Fact]
    public void A_Transfer_Should_Be_Two_Halves_With_One_Id_And_No_Say_In_Income_Or_Expense()
    {
        var (outgoing, incoming) = Transaction.Transfer("Pagamento fatura", 900m, new DateOnly(2026, 9, 5), 1, from: 1, to: 2, Someone);

        outgoing.IsValid.Should().BeTrue();
        outgoing.TransferId.Should().Be(incoming.TransferId);
        outgoing.Type.Should().Be(TransactionType.Expense);
        incoming.Type.Should().Be(TransactionType.Income);
        outgoing.IsTransfer.Should().BeTrue();
    }

    [Fact]
    public void A_Transfer_To_The_Same_Account_Should_Be_Refused()
    {
        var (outgoing, _) = Transaction.Transfer("Erro", 10m, new DateOnly(2026, 9, 5), 1, from: 1, to: 1, Someone);

        outgoing.IsInvalid.Should().BeTrue();
    }

    [Fact]
    public void Installments_Should_Sum_Exactly_And_Step_A_Month_Each()
    {
        var parts = Transaction.Installments("Notebook", 1000m, new DateOnly(2026, 1, 31), TransactionType.Expense, categoryId: 1, accountId: 2, Someone, count: 3);

        parts.Should().HaveCount(3);
        parts.Sum(p => p.Amount).Should().Be(1000m);
        parts.Select(p => p.Amount).Should().Equal([333.34m, 333.33m, 333.33m], "the odd cent goes on the first");
        parts.Select(p => p.Date).Should().Equal(new DateOnly(2026, 1, 31), new DateOnly(2026, 2, 28), new DateOnly(2026, 3, 31));
        parts.Select(p => p.Description).Should().Equal("Notebook (1/3)", "Notebook (2/3)", "Notebook (3/3)");
        parts.Select(p => p.InstallmentGroup).Distinct().Should().ContainSingle();
    }

    [Fact]
    public void One_Installment_Is_Just_A_Transaction()
    {
        var parts = Transaction.Installments("À vista", 100m, new DateOnly(2026, 1, 10), TransactionType.Expense, categoryId: 1, accountId: 2, Someone, count: 1);

        parts.Should().ContainSingle().Which.InstallmentGroup.Should().BeNull();
    }

    [Fact]
    public void A_Category_Rule_Should_Match_The_Same_Merchant_On_A_Different_Day()
    {
        var rule = new CategoryRule("mercado silva", categoryId: 1, Someone);

        rule.Pattern.Should().Be("MERCADO SILVA");
        rule.Matches("PIX ENVIADO 03/09 MERCADO SILVA 0001").Should().BeTrue();
        rule.Matches("PIX ENVIADO 15/09 MERCADO  SILVA 0002").Should().BeTrue();
        rule.Matches("POSTO SHELL").Should().BeFalse();
    }
}
