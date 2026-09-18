using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TF.Models;
using Trust_Finance.IntegrationTests.Infrastructure;

namespace Trust_Finance.IntegrationTests.Api;

public class TransactionEndpointsTests : IntegrationTestBase
{
    private static readonly DateTime Date = new(2026, 3, 14, 0, 0, 0, DateTimeKind.Unspecified);

    public TransactionEndpointsTests(TrustFinanceApiFactory factory) : base(factory) { }

    [Fact]
    public async Task Create_stores_the_transaction_against_the_caller()
    {
        var (user, category) = await SignInWithCategoryAsync();

        var response = await user.Client.PostAsJsonAsync("/api/transactions", new
        {
            description = "Weekly shop",
            amount = 149.90m,
            date = Date,
            categoryId = category.Id
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        await using var context = CreateDbContext();
        var stored = await context.Transactions.SingleAsync();
        stored.UserId.Should().Be(user.Id, "the owner comes from the token, never from the payload");
        stored.CategoryId.Should().Be(category.Id);
        stored.Description.Should().Be("Weekly shop");
    }

    [Theory]
    [InlineData("149.90")]
    [InlineData("0.01")]
    [InlineData("9999999999.99")]
    public async Task Amounts_round_trip_through_the_decimal_18_2_column(string rawAmount)
    {
        var amount = decimal.Parse(rawAmount, System.Globalization.CultureInfo.InvariantCulture);
        var (user, category) = await SignInWithCategoryAsync();

        var created = await user.Client.PostAsJsonAsync("/api/transactions", new
        {
            description = "Precision check",
            amount,
            date = Date,
            categoryId = category.Id
        });

        created.StatusCode.Should().Be(HttpStatusCode.Created);

        var fetched = await user.Client.GetAsync("/api/transactions");
        var transactions = await fetched.ReadDataAsync<List<Transaction>>();

        transactions.Should().ContainSingle()
            .Which.Amount.Should().Be(amount, "SQL Server must not round or truncate the value");
    }

    [Fact]
    public async Task Get_returns_only_the_callers_transactions()
    {
        var (ada, category) = await SignInWithCategoryAsync();
        var bob = await SignUpAsync("bob@trustfinance.dev");
        var bobsCategory = await CreateCategoryAsync(bob.Client, "Rent", "rent");

        await CreateTransactionAsync(ada.Client, category.Id, "Ada's coffee");
        await CreateTransactionAsync(bob.Client, bobsCategory.Id, "Bob's rent");

        var response = await ada.Client.GetAsync("/api/transactions");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.ReadDataAsync<List<Transaction>>())
            .Should().ContainSingle()
            .Which.Description.Should().Be("Ada's coffee");
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("DELETE")]
    public async Task Another_users_transaction_is_invisible(string method)
    {
        var (ada, category) = await SignInWithCategoryAsync();
        var bob = await SignUpAsync("bob@trustfinance.dev");
        var adasTransaction = await CreateTransactionAsync(ada.Client, category.Id, "Ada's coffee");

        var response = await bob.Client.SendAsync(
            new HttpRequestMessage(new HttpMethod(method), $"/api/transactions/{adasTransaction.Id}"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        await using var context = CreateDbContext();
        (await context.Transactions.CountAsync()).Should().Be(1, "Bob must not be able to delete Ada's data");
    }

    [Fact]
    public async Task Another_user_cannot_update_a_transaction()
    {
        var (ada, category) = await SignInWithCategoryAsync();
        var bob = await SignUpAsync("bob@trustfinance.dev");
        var adasTransaction = await CreateTransactionAsync(ada.Client, category.Id, "Ada's coffee");

        var response = await bob.Client.PutAsJsonAsync($"/api/transactions/{adasTransaction.Id}", new
        {
            description = "Hijacked",
            amount = 1m,
            date = Date,
            categoryId = category.Id
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        await using var context = CreateDbContext();
        (await context.Transactions.SingleAsync()).Description.Should().Be("Ada's coffee");
    }

    [Fact]
    public async Task Update_persists_the_new_values_for_the_owner()
    {
        var (ada, category) = await SignInWithCategoryAsync();
        var transaction = await CreateTransactionAsync(ada.Client, category.Id, "Weekly shop");

        var response = await ada.Client.PutAsJsonAsync($"/api/transactions/{transaction.Id}", new
        {
            description = "Monthly shop",
            amount = 320.50m,
            date = Date.AddDays(1),
            categoryId = category.Id
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var context = CreateDbContext();
        var stored = await context.Transactions.SingleAsync();
        stored.Description.Should().Be("Monthly shop");
        stored.Amount.Should().Be(320.50m);
    }

    [Fact]
    public async Task Delete_removes_the_owners_transaction()
    {
        var (ada, category) = await SignInWithCategoryAsync();
        var transaction = await CreateTransactionAsync(ada.Client, category.Id, "Weekly shop");

        var response = await ada.Client.DeleteAsync($"/api/transactions/{transaction.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var context = CreateDbContext();
        (await context.Transactions.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Create_rejects_an_unknown_category_with_400()
    {
        var user = await SignUpAsync("ada@trustfinance.dev");

        var response = await user.Client.PostAsJsonAsync("/api/transactions", new
        {
            description = "Orphan",
            amount = 10m,
            date = Date,
            categoryId = 404404
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "the foreign key would otherwise surface as an unhandled 500");

        await using var context = CreateDbContext();
        (await context.Transactions.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Update_rejects_an_unknown_category_with_400()
    {
        var (ada, category) = await SignInWithCategoryAsync();
        var transaction = await CreateTransactionAsync(ada.Client, category.Id, "Weekly shop");

        var response = await ada.Client.PutAsJsonAsync($"/api/transactions/{transaction.Id}", new
        {
            description = "Weekly shop",
            amount = 10m,
            date = Date,
            categoryId = 404404
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await using var context = CreateDbContext();
        (await context.Transactions.SingleAsync()).CategoryId.Should().Be(category.Id);
    }

    [Theory]
    [InlineData("ab", 10, "Description must be between 3 and 100 characters")]
    [InlineData("Valid description", 0, "Invalid amount")]
    [InlineData("Valid description", -5, "Invalid amount")]
    public async Task Create_rejects_invalid_payloads(string description, decimal amount, string expectedError)
    {
        var (user, category) = await SignInWithCategoryAsync();

        var response = await user.Client.PostAsJsonAsync("/api/transactions", new
        {
            description,
            amount,
            date = Date,
            categoryId = category.Id
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ReadResultAsync<Transaction>()).ErrorMessages.Should().Contain(expectedError);
    }

    [Fact]
    public async Task A_transaction_cannot_point_at_another_users_category()
    {
        var (ada, adasCategory) = await SignInWithCategoryAsync();
        var bob = await SignUpAsync("bob@trustfinance.dev");

        var response = await bob.Client.PostAsJsonAsync("/api/transactions", new
        {
            description = "Borrowed category",
            amount = 10m,
            date = Date,
            categoryId = adasCategory.Id
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ReadResultAsync<Transaction>())
            .ErrorMessages.Should().Contain("Category not found");

        await using var context = CreateDbContext();
        (await context.Transactions.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Deleting_a_category_cascades_to_its_transactions()
    {
        var (ada, category) = await SignInWithCategoryAsync();
        await CreateTransactionAsync(ada.Client, category.Id, "Weekly shop");

        var response = await ada.Client.DeleteAsync($"/api/categories/{category.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var context = CreateDbContext();
        (await context.Transactions.AnyAsync())
            .Should().BeFalse("the FK is declared ON DELETE CASCADE");
    }

    private async Task<(TestUser User, Category Category)> SignInWithCategoryAsync()
    {
        var user = await SignUpAsync("ada@trustfinance.dev");
        var category = await CreateCategoryAsync(user.Client);

        return (user, category);
    }

    private static async Task<Transaction> CreateTransactionAsync(
        HttpClient client,
        int categoryId,
        string description)
    {
        var response = await client.PostAsJsonAsync("/api/transactions", new
        {
            description,
            amount = 42.50m,
            date = Date,
            categoryId
        });

        response.EnsureSuccessStatusCode();

        return await response.ReadDataAsync<Transaction>();
    }
}
