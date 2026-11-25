using BE_ZSM.Contexts;
using BE_ZSM.Entities;
using BE_ZSM.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;

namespace BE_ZSM.Tests;

public sealed class TodoLastWriteWinsApiTests : IAsyncLifetime, IDisposable
{
    private const int TodoId = 801;
    private readonly TestApiFactory _factory = new();

    public async Task InitializeAsync()
    {
        await _factory.SeedUsersAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Todos.Add(new Todo
        {
            Id = TodoId,
            UserId = TestUsers.UserAId,
            Title = "Last write wins todo",
            Description = "Before",
            Status = TodoStatus.Todo,
            Priority = TodoPriority.Medium,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;
    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task OwnerUpdatesWithoutConcurrencyToken_LastWriteWins_AndResponseOmitsRowVersion()
    {
        using var client = _factory.CreateAuthenticatedClient(TestUsers.UserAId);

        var firstUpdate = await UpdateDescriptionAsync(client, "First write");
        var secondUpdate = await UpdateDescriptionAsync(client, "Last write");

        Assert.Equal(HttpStatusCode.OK, firstUpdate.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondUpdate.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var todo = await db.Todos.AsNoTracking().SingleAsync(item => item.Id == TodoId);
        Assert.Equal("Last write", todo.Description);
        Assert.Equal(TestUsers.UserAId, todo.UserId);
        Assert.Equal("Last write wins todo", todo.Title);

        using var response = System.Text.Json.JsonDocument.Parse(await (await client.GetAsync("/api/todos/me")).Content.ReadAsStringAsync());
        var todoJson = response.RootElement.GetProperty("items")[0];
        Assert.False(todoJson.TryGetProperty("rowVersion", out _));
    }

    [Fact]
    public async Task CreateDoesNotRequireConcurrencyToken()
    {
        using var client = _factory.CreateAuthenticatedClient(TestUsers.UserAId);

        var response = await client.PutAsJsonAsync("/api/todos/batch", new[]
        {
            new { title = "New Todo", description = "No concurrency token" }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static Task<HttpResponseMessage> UpdateDescriptionAsync(HttpClient client, string description)
    {
        return client.PutAsJsonAsync("/api/todos/batch", new[]
        {
            new
            {
                id = TodoId,
                title = "Last write wins todo",
                description,
                priority = TodoPriority.Medium.ToString()
            }
        });
    }
}
