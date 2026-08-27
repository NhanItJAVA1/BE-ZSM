using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using BE_ZSM.Contexts;
using BE_ZSM.Entities;
using BE_ZSM.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace BE_ZSM.Tests;

public sealed class TestApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"todo-api-tests-{Guid.NewGuid():N}";

    public TestApiFactory()
    {
        Environment.SetEnvironmentVariable("ACCESS_KEY_ID", "test-access-key");
        Environment.SetEnvironmentVariable("SECRET_ACCESS_KEY", "test-secret-key");
        Environment.SetEnvironmentVariable("AWS_REGION", "ap-southeast-1");
        Environment.SetEnvironmentVariable("JWT_ISSUER", "BE-ZSM-Test");
        Environment.SetEnvironmentVariable("JWT_AUDIENCE", "BE-ZSM-Test-Client");
        Environment.SetEnvironmentVariable("JWT_KEY", "BE-ZSM-Test-Key-For-Integration-Tests-Only-123456789");
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", "Server=(local);Database=TodoTests;Trusted_Connection=True;");
        Environment.SetEnvironmentVariable("ConnectionStrings__Redis", "localhost:6379");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureLogging(logging => logging.ClearProviders());

        builder.ConfigureAppConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=(local);Database=TodoTests;Trusted_Connection=True;",
                ["ConnectionStrings:Redis"] = "localhost:6379"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(_databaseName));
        });
    }

    public HttpClient CreateAuthenticatedClient(int userId)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateJwt(userId));
        return client;
    }

    public async Task SeedUsersAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();

        db.Roles.RemoveRange(db.Roles);
        db.Users.RemoveRange(db.Users);
        await db.SaveChangesAsync();

        db.Roles.AddRange(
            new Role { Id = 1, Name = UserRole.User, Description = "Regular User" },
            new Role { Id = 2, Name = UserRole.Admin, Description = "Administrator" });

        db.Users.AddRange(
            new User
            {
                Id = TestUsers.UserAId,
                Username = "todo-user-a",
                Email = "todo-user-a@example.test",
                PasswordHash = "unused",
                DisplayName = "Todo User A",
                RoleId = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new User
            {
                Id = TestUsers.UserBId,
                Username = "todo-user-b",
                Email = "todo-user-b@example.test",
                PasswordHash = "unused",
                DisplayName = "Todo User B",
                RoleId = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

        await db.SaveChangesAsync();
    }

    private static string CreateJwt(int userId)
    {
        var issuer = Environment.GetEnvironmentVariable("JWT_ISSUER")!;
        var audience = Environment.GetEnvironmentVariable("JWT_AUDIENCE")!;
        var key = Environment.GetEnvironmentVariable("JWT_KEY")!;

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, $"todo-user-{userId}"),
            new Claim(ClaimTypes.Role, "User")
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public abstract class IntegrationTestBase : IAsyncLifetime, IDisposable
{
    protected readonly TestApiFactory Factory = new();
    protected HttpClient UserAClient = null!;
    protected HttpClient UserBClient = null!;
    protected JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web);

    public async Task InitializeAsync()
    {
        await Factory.SeedUsersAsync();
        UserAClient = Factory.CreateAuthenticatedClient(TestUsers.UserAId);
        UserBClient = Factory.CreateAuthenticatedClient(TestUsers.UserBId);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        UserAClient?.Dispose();
        UserBClient?.Dispose();
        Factory.Dispose();
    }

    protected static object TodoRequest(
        string title,
        string? description = null,
        TodoPriority? priority = null,
        DateTime? dueDate = null,
        int? categoryId = null)
    {
        return new
        {
            title,
            description,
            priority,
            dueDate,
            categoryId
        };
    }

    protected async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<JsonElement>(json, JsonOptions);
    }

    protected async Task<JsonElement> CreateTodoAsync(
        HttpClient client,
        string title,
        string? description = null,
        TodoPriority? priority = null,
        DateTime? dueDate = null,
        int? categoryId = null)
    {
        var response = await client.PostAsJsonAsync("/api/todos", new[]
        {
            TodoRequest(title, description, priority, dueDate, categoryId)
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await GetSingleTodoByTitleAsync(client, title);
    }

    protected async Task<JsonElement> CreateCategoryAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/todo-categories", new { name }, JsonOptions);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var categoriesResponse = await client.GetAsync("/api/todo-categories");
        Assert.Equal(HttpStatusCode.OK, categoriesResponse.StatusCode);

        var categories = await ReadJsonAsync(categoriesResponse);
        return categories.EnumerateArray().Single(c => c.GetProperty("name").GetString() == name);
    }

    protected async Task<JsonElement> GetSingleTodoByTitleAsync(HttpClient client, string title)
    {
        var response = await client.GetAsync($"/api/todos?search={Uri.EscapeDataString(title)}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await ReadJsonAsync(response);
        return page.GetProperty("items").EnumerateArray()
            .Single(t => t.GetProperty("title").GetString() == title);
    }

    protected static int Id(JsonElement element) => element.GetProperty("id").GetInt32();

    protected static string ErrorCode(JsonElement element) => element.GetProperty("errorCode").GetString()!;
}

public static class TestUsers
{
    public const int UserAId = 1001;
    public const int UserBId = 1002;
}
