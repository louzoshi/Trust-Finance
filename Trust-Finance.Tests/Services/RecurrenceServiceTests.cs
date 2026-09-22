using TrustFinance.App;
using TrustFinance.App.Services;
using TrustFinance.Domain.Entities;
using TrustFinance.Tests.Fixtures;

namespace TrustFinance.Tests.Services;

public class RecurrenceServiceTests : IDisposable
{
    private readonly SqliteDatabase _db = new();
    private readonly FixedClock _clock = new(new DateOnly(2026, 9, 20));
    private readonly RecurrenceService _service;
    private readonly TransactionService _transactions;

    public RecurrenceServiceTests()
    {
        _service = new RecurrenceService(_db, _clock);
        _transactions = new TransactionService(_db);
    }

    public void Dispose() => _db.Dispose();

    private RecurringTransaction Salary(int categoryId, int userId, DateOnly start, DateOnly? end = null)
        => new("Salário", 5000m, TransactionType.Income, categoryId, _db.AccountFor(userId), Frequency.Monthly, start, end, userId);

    [Fact]
    public async Task Create_Should_Store_The_Recurrence_Without_Posting_Anything()
    {
        var category = await _db.AddCategoryAsync(_db.Ada);

        var result = await _service.CreateAsync(Salary(category.Id, _db.Ada, new DateOnly(2026, 9, 5)));

        result.Success.Should().BeTrue();
        result.Value!.Id.Should().BePositive();
        (await _transactions.GetAllAsync(_db.Ada)).Should().BeEmpty("posting is a separate, explicit step");
    }

    [Fact]
    public async Task Create_Should_Reject_A_Category_Owned_By_Somebody_Else()
    {
        var bobs = await _db.AddCategoryAsync(_db.Bob);

        var result = await _service.CreateAsync(Salary(bobs.Id, _db.Ada, new DateOnly(2026, 9, 5)));

        result.Failed.Should().BeTrue();
        result.Messages.Should().ContainSingle().Which.Should().Contain("Categoria não encontrada");
    }

    [Fact]
    public async Task PostDue_Should_Write_Every_Due_Occurrence_As_A_Transaction()
    {
        var category = await _db.AddCategoryAsync(_db.Ada);
        var created = (await _service.CreateAsync(Salary(category.Id, _db.Ada, new DateOnly(2026, 7, 5)))).Value!;

        var posted = await _service.PostDueAsync(_db.Ada);

        posted.Should().Be(3);
        var rows = await _transactions.GetAllAsync(_db.Ada);
        rows.Select(t => t.Date).Should().Equal(new DateOnly(2026, 9, 5), new DateOnly(2026, 8, 5), new DateOnly(2026, 7, 5));
        rows.Should().OnlyContain(t => t.RecurringTransactionId == created.Id && t.Amount == 5000m);
        (await _service.GetByIdAsync(created.Id, _db.Ada))!.NextDate.Should().Be(new DateOnly(2026, 10, 5));
    }

    [Fact]
    public async Task PostDue_Should_Post_Nothing_The_Second_Time()
    {
        var category = await _db.AddCategoryAsync(_db.Ada);
        await _service.CreateAsync(Salary(category.Id, _db.Ada, new DateOnly(2026, 9, 5)));

        await _service.PostDueAsync(_db.Ada);
        var again = await _service.PostDueAsync(_db.Ada);

        again.Should().Be(0);
        (await _transactions.GetAllAsync(_db.Ada)).Should().HaveCount(1);
    }

    [Fact]
    public async Task PostDue_Should_Post_The_Next_One_When_The_Day_Comes()
    {
        var category = await _db.AddCategoryAsync(_db.Ada);
        await _service.CreateAsync(Salary(category.Id, _db.Ada, new DateOnly(2026, 9, 5)));
        await _service.PostDueAsync(_db.Ada);

        _clock.Today = new DateOnly(2026, 10, 4);
        (await _service.PostDueAsync(_db.Ada)).Should().Be(0);

        _clock.Today = new DateOnly(2026, 10, 5);
        (await _service.PostDueAsync(_db.Ada)).Should().Be(1);
    }

    [Fact]
    public async Task PostDue_Should_Only_Touch_The_Callers_Recurrences()
    {
        var adas = await _db.AddCategoryAsync(_db.Ada);
        var bobs = await _db.AddCategoryAsync(_db.Bob);
        await _service.CreateAsync(Salary(adas.Id, _db.Ada, new DateOnly(2026, 9, 5)));
        await _service.CreateAsync(Salary(bobs.Id, _db.Bob, new DateOnly(2026, 9, 5)));

        await _service.PostDueAsync(_db.Ada);

        (await _transactions.GetAllAsync(_db.Ada)).Should().HaveCount(1);
        (await _transactions.GetAllAsync(_db.Bob)).Should().BeEmpty();
    }

    [Fact]
    public async Task Deleting_A_Recurrence_Should_Keep_What_It_Posted()
    {
        var category = await _db.AddCategoryAsync(_db.Ada);
        var created = (await _service.CreateAsync(Salary(category.Id, _db.Ada, new DateOnly(2026, 9, 5)))).Value!;
        await _service.PostDueAsync(_db.Ada);

        (await _service.DeleteAsync(created.Id, _db.Ada)).Success.Should().BeTrue();

        var row = (await _transactions.GetAllAsync(_db.Ada)).Single();
        row.Amount.Should().Be(5000m);
        row.RecurringTransactionId.Should().BeNull("the database sets the link to null rather than cascading");
    }

    [Fact]
    public async Task Delete_Should_Not_Reach_Another_Users_Recurrence()
    {
        var category = await _db.AddCategoryAsync(_db.Ada);
        var created = (await _service.CreateAsync(Salary(category.Id, _db.Ada, new DateOnly(2026, 9, 5)))).Value!;

        (await _service.DeleteAsync(created.Id, _db.Bob)).Failed.Should().BeTrue();
        (await _service.GetByIdAsync(created.Id, _db.Ada)).Should().NotBeNull();
    }

    [Fact]
    public async Task Update_Should_Change_The_Amount_For_Future_Occurrences_Only()
    {
        var category = await _db.AddCategoryAsync(_db.Ada);
        var created = (await _service.CreateAsync(Salary(category.Id, _db.Ada, new DateOnly(2026, 9, 5)))).Value!;
        await _service.PostDueAsync(_db.Ada);

        var corrected = new RecurringTransaction("Salário", 5500m, TransactionType.Income, category.Id,
            _db.AdaAccount, Frequency.Monthly, new DateOnly(2026, 9, 5), null, _db.Ada);
        // Posting the September occurrence above already wrote the recurrence, so the
        // correction is made against the version that write left behind.
        var current = (await _service.GetByIdAsync(created.Id, _db.Ada))!;
        (await _service.UpdateAsync(created.Id, corrected, current.Version)).Success.Should().BeTrue();

        _clock.Today = new DateOnly(2026, 10, 5);
        await _service.PostDueAsync(_db.Ada);

        var rows = await _transactions.GetAllAsync(_db.Ada);
        rows.Select(t => t.Amount).Should().Equal(5500m, 5000m);
    }

    [Fact]
    public async Task Deleting_A_Category_Should_Delete_Its_Recurrences()
    {
        var category = await _db.AddCategoryAsync(_db.Ada);
        await _service.CreateAsync(Salary(category.Id, _db.Ada, new DateOnly(2026, 9, 5)));

        (await new CategoryService(_db).DeleteAsync(category.Id, _db.Ada)).Success.Should().BeTrue();

        (await _service.GetAllAsync(_db.Ada)).Should().BeEmpty();
    }
}
