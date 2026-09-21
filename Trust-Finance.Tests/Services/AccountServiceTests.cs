using TrustFinance.App.Services;
using TrustFinance.Domain.Entities;
using TrustFinance.Tests.Fixtures;

namespace TrustFinance.Tests.Services;

public class AccountServiceTests : IDisposable
{
    private readonly SqliteDatabase _db = new();
    private readonly FixedClock _clock = new(new DateOnly(2026, 9, 20));
    private readonly AccountService _accounts;
    private readonly TransactionService _transactions;

    public AccountServiceTests()
    {
        _accounts = new AccountService(_db, _clock);
        _transactions = new TransactionService(_db);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task A_User_With_No_Accounts_Should_Get_One_On_First_Use()
    {
        // The fixture opens one for Ada; a third user proves the on-demand path.
        await using var db = _db.CreateDbContext();
        var carla = new User("Carla", "carla@example.com", "hash");
        db.Users.Add(carla);
        await db.SaveChangesAsync();

        var accounts = await _accounts.GetAllAsync(carla.Id);

        accounts.Should().ContainSingle().Which.Kind.Should().Be(AccountKind.Checking);
        (await _accounts.GetAllAsync(carla.Id)).Should().ContainSingle("the second call finds the first one, it does not open another");
    }

    [Fact]
    public async Task Two_Accounts_Cannot_Share_A_Name()
    {
        var result = await _accounts.CreateAsync(new Account("Conta corrente", AccountKind.Savings, _db.Ada));

        result.Failed.Should().BeTrue();
        result.Messages.Should().ContainSingle().Which.Should().Contain("Já existe");
    }

    [Fact]
    public async Task The_Same_Name_Is_Fine_For_Another_User()
    {
        var result = await _accounts.CreateAsync(new Account("Conta corrente", AccountKind.Savings, _db.Bob));

        result.Failed.Should().BeTrue("Bob already has one too");

        (await _accounts.CreateAsync(new Account("Nubank", AccountKind.CreditCard, _db.Bob, 10, 17))).Success.Should().BeTrue();
        (await _accounts.CreateAsync(new Account("Nubank", AccountKind.CreditCard, _db.Ada, 10, 17))).Success.Should().BeTrue();
    }

    [Fact]
    public async Task An_Account_With_Transactions_Cannot_Be_Deleted()
    {
        var category = await _db.AddCategoryAsync(_db.Ada);
        await _db.AddTransactionAsync(_db.Ada, category.Id);

        var result = await _accounts.DeleteAsync(_db.AdaAccount, _db.Ada);

        result.Failed.Should().BeTrue();
        result.Messages.Should().ContainSingle().Which.Should().Contain("lançamentos");
    }

    [Fact]
    public async Task The_Last_Account_Cannot_Be_Deleted()
    {
        var result = await _accounts.DeleteAsync(_db.AdaAccount, _db.Ada);

        result.Failed.Should().BeTrue();
        result.Messages.Should().ContainSingle().Which.Should().Contain("pelo menos uma conta");
    }

    [Fact]
    public async Task Delete_Should_Not_Reach_Another_Users_Account()
    {
        (await _accounts.DeleteAsync(_db.BobAccount, _db.Ada)).Failed.Should().BeTrue();
        (await _accounts.GetByIdAsync(_db.BobAccount, _db.Bob)).Should().NotBeNull();
    }

    [Fact]
    public async Task A_Card_With_History_Cannot_Become_A_Checking_Account()
    {
        var card = await _db.AddCardAsync(_db.Ada);
        var category = await _db.AddCategoryAsync(_db.Ada);
        await _db.AddTransactionAsync(_db.Ada, category.Id, accountId: card.Id);

        var result = await _accounts.UpdateAsync(card.Id, new Account("Cartão", AccountKind.Checking, _db.Ada));

        result.Failed.Should().BeTrue();
        result.Messages.Should().ContainSingle().Which.Should().Contain("cartão");
    }

    [Fact]
    public async Task Balances_Should_Read_Cash_As_Positive_And_A_Card_As_What_Is_Owed()
    {
        var card = await _db.AddCardAsync(_db.Ada);
        var category = await _db.AddCategoryAsync(_db.Ada);

        await _db.AddTransactionAsync(_db.Ada, category.Id, 5000m, TransactionType.Income);
        await _db.AddTransactionAsync(_db.Ada, category.Id, 200m, TransactionType.Expense);
        await _db.AddTransactionAsync(_db.Ada, category.Id, 350m, TransactionType.Expense, accountId: card.Id);

        var balances = await _accounts.BalancesAsync(_db.Ada);

        balances.Single(b => b.Account.Id == _db.AdaAccount).Balance.Should().Be(4800m);
        balances.Single(b => b.Account.Id == card.Id).Balance.Should().Be(-350m, "a card balance is a debt");
    }

    [Fact]
    public async Task A_Card_Statement_Should_Group_Purchases_And_Be_Settled_By_The_Payment()
    {
        var card = await _db.AddCardAsync(_db.Ada, closingDay: 10, dueDay: 17);
        var category = await _db.AddCategoryAsync(_db.Ada);

        // Two purchases on August's statement, one after it closed.
        await _db.AddTransactionAsync(_db.Ada, category.Id, 100m, date: new DateOnly(2026, 8, 5), accountId: card.Id);
        await _db.AddTransactionAsync(_db.Ada, category.Id, 250m, date: new DateOnly(2026, 8, 10), accountId: card.Id);
        await _db.AddTransactionAsync(_db.Ada, category.Id, 40m, date: new DateOnly(2026, 8, 11), accountId: card.Id);

        // Paid on the due date, from checking.
        var (outgoing, incoming) = Transaction.Transfer("Fatura", 350m, new DateOnly(2026, 8, 17), category.Id, _db.AdaAccount, card.Id, _db.Ada);
        (await _transactions.CreateTransferAsync(outgoing, incoming)).Success.Should().BeTrue();

        var statements = await _accounts.StatementsAsync(card.Id, _db.Ada);

        var august = statements.Single(s => s.Closing == new DateOnly(2026, 8, 10));
        august.Purchases.Should().Be(350m);
        august.Payments.Should().Be(350m);
        august.IsPaid.Should().BeTrue();

        var september = statements.Single(s => s.Closing == new DateOnly(2026, 9, 10));
        september.Purchases.Should().Be(40m, "the purchase after closing rolled into the next statement");
        september.Balance.Should().Be(40m);
        september.Due.Should().Be(new DateOnly(2026, 9, 17));
    }

    [Fact]
    public async Task Statements_Should_Be_Empty_For_Something_That_Is_Not_A_Card()
    {
        (await _accounts.StatementsAsync(_db.AdaAccount, _db.Ada)).Should().BeEmpty();
    }

    [Fact]
    public async Task A_Transfer_Should_Be_Deleted_On_Both_Sides()
    {
        var card = await _db.AddCardAsync(_db.Ada);
        var category = await _db.AddCategoryAsync(_db.Ada);

        var (outgoing, incoming) = Transaction.Transfer("Fatura", 100m, new DateOnly(2026, 9, 5), category.Id, _db.AdaAccount, card.Id, _db.Ada);
        await _transactions.CreateTransferAsync(outgoing, incoming);
        (await _transactions.GetAllAsync(_db.Ada)).Should().HaveCount(2);

        await _transactions.DeleteAsync(outgoing.Id, _db.Ada);

        (await _transactions.GetAllAsync(_db.Ada)).Should().BeEmpty("deleting one half would leave money that arrived from nowhere");
    }

    [Fact]
    public async Task A_Transaction_Should_Not_Reach_Another_Users_Account()
    {
        var category = await _db.AddCategoryAsync(_db.Ada);

        var result = await _transactions.CreateAsync(
            new Transaction("Compra", 10m, new DateOnly(2026, 9, 5), TransactionType.Expense, category.Id, _db.BobAccount, _db.Ada));

        result.Failed.Should().BeTrue();
        result.Messages.Should().ContainSingle().Which.Should().Contain("Conta não encontrada");
    }
}
