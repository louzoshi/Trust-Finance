using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TF.Models;
using Trust_Finance.IntegrationTests.Infrastructure;

namespace Trust_Finance.IntegrationTests.Api;

public class CategoryEndpointsTests : IntegrationTestBase
{
    private TestUser _user = null!;

    public CategoryEndpointsTests(TrustFinanceApiFactory factory) : base(factory) { }

    private async Task<HttpClient> SignedInAsync()
        => (_user ??= await SignUpAsync("ada@trustfinance.dev")).Client;

    [Fact]
    public async Task Create_then_read_round_trips_through_the_database()
    {
        var client = await SignedInAsync();

        var created = await client.PostAsJsonAsync(
            "/api/categories",
            new { name = "Groceries", slug = "groceries" });

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var category = await created.ReadDataAsync<Category>();
        category.Id.Should().BeGreaterThan(0);

        var fetched = await client.GetAsync($"/api/categories/{category.Id}");
        fetched.StatusCode.Should().Be(HttpStatusCode.OK);
        (await fetched.ReadDataAsync<Category>()).Name.Should().Be("Groceries");

        await using var context = CreateDbContext();
        (await context.Categories.SingleAsync()).Slug.Should().Be("groceries");
    }

    [Fact]
    public async Task Get_lists_every_category()
    {
        var client = await SignedInAsync();
        await CreateCategoryAsync(client, "Groceries", "groceries");
        await CreateCategoryAsync(client, "Rent", "rent");

        var response = await client.GetAsync("/api/categories");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.ReadDataAsync<List<Category>>())
            .Should().HaveCount(2)
            .And.OnlyHaveUniqueItems(c => c.Slug);
    }

    [Fact]
    public async Task Create_rejects_a_slug_that_is_already_taken()
    {
        var client = await SignedInAsync();
        await CreateCategoryAsync(client, "Groceries", "groceries");

        var duplicate = await client.PostAsJsonAsync(
            "/api/categories",
            new { name = "Supermarket", slug = "groceries" });

        duplicate.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await duplicate.ReadResultAsync<Category>()).ErrorMessages
            .Should().Contain(e => e.Contains("Slug already exists"));

        await using var context = CreateDbContext();
        (await context.Categories.CountAsync()).Should().Be(1);
    }

    [Theory]
    [InlineData("ab", "too-short-name", "Name must be between 3 and 40 characters")]
    [InlineData("Groceries", "", "Slug is required")]
    public async Task Create_rejects_invalid_payloads(string name, string slug, string expectedError)
    {
        var client = await SignedInAsync();

        var response = await client.PostAsJsonAsync("/api/categories", new { name, slug });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ReadResultAsync<Category>()).ErrorMessages.Should().Contain(expectedError);
    }

    [Fact]
    public async Task Update_persists_the_new_name_and_slug()
    {
        var client = await SignedInAsync();
        var category = await CreateCategoryAsync(client, "Groceries", "groceries");

        var response = await client.PutAsJsonAsync(
            $"/api/categories/{category.Id}",
            new { name = "Supermarket", slug = "supermarket" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var context = CreateDbContext();
        var stored = await context.Categories.SingleAsync(c => c.Id == category.Id);
        stored.Name.Should().Be("Supermarket");
        stored.Slug.Should().Be("supermarket");
    }

    [Fact]
    public async Task Update_rejects_a_slug_owned_by_another_category()
    {
        var client = await SignedInAsync();
        await CreateCategoryAsync(client, "Groceries", "groceries");
        var rent = await CreateCategoryAsync(client, "Rent", "rent");

        var response = await client.PutAsJsonAsync(
            $"/api/categories/{rent.Id}",
            new { name = "Rent", slug = "groceries" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "the unique index on Slug would otherwise blow up as a 500");

        await using var context = CreateDbContext();
        (await context.Categories.SingleAsync(c => c.Id == rent.Id)).Slug.Should().Be("rent");
    }

    [Fact]
    public async Task Update_keeps_working_when_the_slug_is_unchanged()
    {
        var client = await SignedInAsync();
        var category = await CreateCategoryAsync(client, "Groceries", "groceries");

        var response = await client.PutAsJsonAsync(
            $"/api/categories/{category.Id}",
            new { name = "Groceries & Home", slug = "groceries" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.ReadDataAsync<Category>()).Name.Should().Be("Groceries & Home");
    }

    [Fact]
    public async Task Delete_removes_the_category()
    {
        var client = await SignedInAsync();
        var category = await CreateCategoryAsync(client);

        var response = await client.DeleteAsync($"/api/categories/{category.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var context = CreateDbContext();
        (await context.Categories.AnyAsync()).Should().BeFalse();
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("DELETE")]
    public async Task Reading_or_deleting_a_missing_category_returns_404(string method)
    {
        var client = await SignedInAsync();

        var response = await client.SendAsync(
            new HttpRequestMessage(new HttpMethod(method), "/api/categories/404404"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.ReadResultAsync<Category>()).ErrorMessages.Should().Contain("Category not found");
    }

    [Fact]
    public async Task Updating_a_missing_category_returns_404()
    {
        var client = await SignedInAsync();

        var response = await client.PutAsJsonAsync(
            "/api/categories/404404",
            new { name = "Ghost", slug = "ghost" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
