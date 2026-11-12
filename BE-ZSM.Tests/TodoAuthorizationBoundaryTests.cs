using BE_ZSM.Authorization;
using BE_ZSM.Contexts;
using BE_ZSM.Entities;
using BE_ZSM.Entities.Rbac;
using BE_ZSM.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;

namespace BE_ZSM.Tests;

public sealed class TodoAuthorizationBoundaryTests : IAsyncLifetime, IDisposable
{
    private readonly TestApiFactory _factory = new();

    public async Task InitializeAsync() => await _factory.SeedUsersAsync();
    public Task DisposeAsync() => Task.CompletedTask;
    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task TodosRead_CanReadOnlyOwnTodos()
    {
        await SeedTodosAsync();
        using var client = _factory.CreateAuthenticatedClient(TestUsers.UserAId);

        var own = await client.GetFromJsonAsync<PagedTodos>("/api/todos/me");
        var otherUser = await client.GetAsync($"/api/todos/users/{TestUsers.UserBId}");

        Assert.Equal(new[] { 101, 102 }, own!.Items.Select(todo => todo.Id));
        Assert.Equal(HttpStatusCode.Forbidden, otherUser.StatusCode);
    }

    [Fact]
    public async Task TodosReadAll_CanReadOtherUsersAndAllTodos()
    {
        await SeedTodosAsync();
        await GrantReadAllToUserAAsync();
        using var client = _factory.CreateAuthenticatedClient(TestUsers.UserAId);

        var anotherUsersTodos = await client.GetFromJsonAsync<PagedTodos>($"/api/todos/users/{TestUsers.UserBId}");
        var allTodos = await client.GetFromJsonAsync<PagedTodos>("/api/todos");

        Assert.Equal(new[] { 201 }, anotherUsersTodos!.Items.Select(todo => todo.Id));
        Assert.Equal(3, allTodos!.TotalItems);
        Assert.Equal(new[] { 201, 101, 102 }, allTodos.Items.Select(todo => todo.Id));
    }

    [Fact]
    public async Task TodosReadAll_WithoutPermissionIsForbidden_AndDoesNotGrantWrites()
    {
        await SeedTodosAsync();
        await ReplaceUserARolesWithReadAllOnlyAsync();
        using var client = _factory.CreateAuthenticatedClient(TestUsers.UserAId);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/todos")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync("/api/todos/batch", new[]
        {
            new { id = 101, title = "Changed", priority = TodoPriority.High.ToString() }
        })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync("/api/todos/batch", new[]
        {
            new { id = 101, isDeleted = true }
        })).StatusCode);
    }

    [Fact]
    public async Task UserSummary_IncludesUsersWithNoTodos_SearchesAndPaginates()
    {
        await SeedTodosAsync();
        await GrantReadAllToUserAAsync();
        using var client = _factory.CreateAuthenticatedClient(TestUsers.UserAId);

        var pageOne = await client.GetFromJsonAsync<PagedUsers>("/api/todos/users/summary?page=1&pageSize=1");
        var pageTwo = await client.GetFromJsonAsync<PagedUsers>("/api/todos/users/summary?page=2&pageSize=1");
        var search = await client.GetFromJsonAsync<PagedUsers>("/api/todos/users/summary?search=todo-user-b");
        var capped = await client.GetFromJsonAsync<PagedUsers>("/api/todos/users/summary?page=-2&pageSize=500");

        Assert.Equal(2, pageOne!.TotalItems);
        Assert.Equal(1, pageOne.PageSize);
        Assert.NotEqual(pageOne.Items[0].UserId, pageTwo!.Items[0].UserId);
        Assert.Equal(TestUsers.UserBId, search!.Items.Single().UserId);
        Assert.Equal(1, search.Items.Single().TodoCount);
        Assert.Equal(1, capped!.Page);
        Assert.Equal(100, capped.PageSize);
    }

    [Fact]
    public async Task SummaryIncludesZeroTodoUsers_AndTodosPaginationIsStable()
    {
        await SeedTodosAsync(onlyUserA: true);
        await GrantReadAllToUserAAsync();
        using var client = _factory.CreateAuthenticatedClient(TestUsers.UserAId);

        var summary = await client.GetFromJsonAsync<PagedUsers>("/api/todos/users/summary");
        var firstPage = await client.GetFromJsonAsync<PagedTodos>("/api/todos?page=1&pageSize=1");
        var secondPage = await client.GetFromJsonAsync<PagedTodos>("/api/todos?page=2&pageSize=1");
        var zeroTodoUser = await client.GetFromJsonAsync<PagedTodos>($"/api/todos/users/{TestUsers.UserBId}");

        Assert.Contains(summary!.Items, user => user.UserId == TestUsers.UserBId && user.TodoCount == 0);
        Assert.Equal(2, firstPage!.TotalItems);
        Assert.NotEqual(firstPage.Items[0].Id, secondPage!.Items[0].Id);
        Assert.Empty(zeroTodoUser!.Items);
        Assert.Equal(0, zeroTodoUser.TotalItems);
    }

    [Fact]
    public async Task ReadAllEndpoints_RequireAuthenticationAndExistingTargetUser()
    {
        using var anonymous = _factory.CreateClient();
        using var ordinary = _factory.CreateAuthenticatedClient(TestUsers.UserAId);

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/todos")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ordinary.GetAsync("/api/todos")).StatusCode);
        await GrantReadAllToUserAAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await ordinary.GetAsync("/api/todos/users/99999")).StatusCode);
    }

    private async Task SeedTodosAsync(bool onlyUserA = false)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Todos.AddRange(new Todo
        {
            Id = 101, UserId = TestUsers.UserAId, Title = "A todo", Status = TodoStatus.Todo,
            Priority = TodoPriority.Medium, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        }, new Todo
        {
            Id = 102, UserId = TestUsers.UserAId, Title = "A second todo", Status = TodoStatus.Todo,
            Priority = TodoPriority.Medium, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        if (!onlyUserA)
            db.Todos.Add(new Todo
            {
                Id = 201, UserId = TestUsers.UserBId, Title = "B todo", Status = TodoStatus.Todo,
                Priority = TodoPriority.Medium, CreatedAt = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc)
            });
        await db.SaveChangesAsync();
    }

    private async Task GrantReadAllToUserAAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var permissionId = await db.Permissions.Where(permission => permission.Code == PermissionCodes.TodosReadAll)
            .Select(permission => permission.Id).SingleAsync();
        db.RolePermissions.Add(new RolePermission { RoleId = 1, PermissionId = permissionId });
        await db.SaveChangesAsync();
    }

    private async Task ReplaceUserARolesWithReadAllOnlyAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Roles.Add(new Role { Id = 3, Name = "TodoReader", Description = "Read all Todos only" });
        var readAllId = await db.Permissions.Where(permission => permission.Code == PermissionCodes.TodosReadAll)
            .Select(permission => permission.Id).SingleAsync();
        db.RolePermissions.Add(new RolePermission { RoleId = 3, PermissionId = readAllId });
        db.UserRoles.Remove(await db.UserRoles.SingleAsync(item => item.UserId == TestUsers.UserAId && item.RoleId == 1));
        db.UserRoles.Add(new UserRole { UserId = TestUsers.UserAId, RoleId = 3 });
        await db.SaveChangesAsync();
    }

    private sealed class PagedTodos
    {
        public List<TodoItem> Items { get; set; } = [];
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalItems { get; set; }
    }

    private sealed class TodoItem
    {
        public int Id { get; set; }
    }

    private sealed class PagedUsers
    {
        public List<UserSummary> Items { get; set; } = [];
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalItems { get; set; }
    }

    private sealed class UserSummary
    {
        public int UserId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int TodoCount { get; set; }
    }
}
