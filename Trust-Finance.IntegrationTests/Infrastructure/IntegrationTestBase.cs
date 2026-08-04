using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using TF.Data;
using TF.Models;

namespace Trust_Finance.IntegrationTests.Infrastructure;

/// <summary>
/// Base for every API test: hands out HTTP clients against the running API and truncates
/// the database before each test so cases never leak state into one another.
/// </summary>
[Collection(ApiTestCollection.Name)]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected IntegrationTestBase(TrustFinanceApiFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
    }

    protected TrustFinanceApiFactory Factory { get; }

    /// <summary>Anonymous client — no Authorization header.</summary>
    protected HttpClient Client { get; }

    public Task InitializeAsync() => Factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    protected TFDataContext CreateDbContext() => Factory.CreateDbContext();

    protected static object RegisterPayload(
        string email,
        string password = "Str0ngPass1",
        string name = "Test User",
        string? slug = null)
        => new
        {
            name,
            email,
            password,
            image = "https://cdn.trustfinance.dev/avatar.png",
            slug = slug ?? email.Split('@')[0]
        };

    protected Task<HttpResponseMessage> RegisterAsync(string email, string password = "Str0ngPass1")
        => Client.PostAsJsonAsync("/api/account/register", RegisterPayload(email, password));

    /// <summary>
    /// Registers a user through the API, optionally promotes it, then signs in and returns
    /// a client with the resulting bearer token attached.
    /// </summary>
    protected async Task<TestUser> SignUpAsync(
        string email = "owner@trustfinance.dev",
        string password = "Str0ngPass1",
        string role = "user")
    {
        var registration = await RegisterAsync(email, password);
        registration.EnsureSuccessStatusCode();

        var user = await registration.ReadDataAsync<User>();

        if (role != "user")
        {
            // Registration always creates a plain user; elevate directly in the database so
            // the token issued below carries the role we want to exercise.
            await using var context = CreateDbContext();
            await context.Users
                .Where(u => u.Id == user.Id)
                .ExecuteUpdateAsync(set => set.SetProperty(u => u.Role, role));
        }

        var login = await Client.PostAsJsonAsync("/api/account/login", new { email, password });
        login.EnsureSuccessStatusCode();

        var token = await login.ReadDataAsync<string>();

        var authenticated = Factory.CreateClient();
        authenticated.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return new TestUser(user.Id, email, password, token, authenticated);
    }

    /// <summary>Creates a category through the API and returns it.</summary>
    protected static async Task<Category> CreateCategoryAsync(
        HttpClient client,
        string name = "Groceries",
        string slug = "groceries")
    {
        var response = await client.PostAsJsonAsync("/api/categories", new { name, slug });
        response.EnsureSuccessStatusCode();

        return await response.ReadDataAsync<Category>();
    }

    protected sealed record TestUser(int Id, string Email, string Password, string Token, HttpClient Client);
}
