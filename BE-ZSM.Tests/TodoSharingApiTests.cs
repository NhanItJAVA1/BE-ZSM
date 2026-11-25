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

public sealed class TodoSharingApiTests : IAsyncLifetime, IDisposable
{
    private const int TodoId = 701;
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
            Title = "Shared todo",
            Status = TodoStatus.Todo,
            Priority = TodoPriority.Medium,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;
    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task OwnerCanShareAndRevoke_SharedUserCanReadAndAddNotes_ButCannotWriteOrReshare()
    {
        await GrantPermissionAsync(TestUsers.UserAId, PermissionCodes.TodosShare);
        await GrantPermissionAsync(TestUsers.UserBId, PermissionCodes.TodosReadShared);
        await GrantPermissionAsync(TestUsers.UserBId, PermissionCodes.TodosNotesCreate);
        using var owner = _factory.CreateAuthenticatedClient(TestUsers.UserAId);
        using var shared = _factory.CreateAuthenticatedClient(TestUsers.UserBId);
        var replace = await owner.PutAsJsonAsync($"/api/todos/{TodoId}/shares", new { userIds = new[] { TestUsers.UserBId, TestUsers.UserBId } });
        Assert.Equal(HttpStatusCode.OK, replace.StatusCode);
        var recipients = await owner.GetFromJsonAsync<List<ShareItem>>($"/api/todos/{TodoId}/shares");
        Assert.Equal(TestUsers.UserBId, Assert.Single(recipients!).SharedWithUserId);

        var sharedTodos = await shared.GetFromJsonAsync<PagedTodos>("/api/todos/shared");
        Assert.Equal(TodoId, Assert.Single(sharedTodos!.Items).Id);
        Assert.Equal("Todo User A", Assert.Single(sharedTodos.Items).OwnerDisplayName);
        Assert.Empty(await shared.GetFromJsonAsync<List<NoteItem>>($"/api/todos/{TodoId}/notes") ?? []);

        var addNote = await shared.PostAsJsonAsync($"/api/todos/{TodoId}/notes", new { content = "Shared note" });
        Assert.Equal(HttpStatusCode.OK, addNote.StatusCode);
        var createdNote = (await addNote.Content.ReadFromJsonAsync<NoteItem>())!;
        Assert.Equal("Shared note", createdNote.Content);
        Assert.Equal("Todo User B", createdNote.DisplayName);

        var listedNotes = await shared.GetFromJsonAsync<List<NoteItem>>($"/api/todos/{TodoId}/notes");
        Assert.Equal("Todo User B", Assert.Single(listedNotes!).DisplayName);
        var ownerUpdate = await owner.PutAsJsonAsync("/api/todos/batch", new[]
        {
            new { id = TodoId, title = "Shared todo", description = "Updated after note", priority = "Medium" }
        });
        Assert.Equal(HttpStatusCode.OK, ownerUpdate.StatusCode);
        var reread = await owner.GetFromJsonAsync<PagedTodos>("/api/todos/me");
        Assert.Equal("Updated after note", Assert.Single(reread!.Items).Description);

        var update = await shared.PutAsJsonAsync("/api/todos/batch", new[]
        {
            new { id = TodoId, title = "Changed by shared user", priority = "High" }
        });
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await shared.PutAsJsonAsync("/api/todos/batch", new[] { new { id = TodoId, title = "ignored", isDeleted = true } })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await shared.PutAsJsonAsync($"/api/todos/{TodoId}/shares", new { userIds = Array.Empty<int>() })).StatusCode);

        Assert.Equal(HttpStatusCode.OK,
            (await owner.PutAsJsonAsync($"/api/todos/{TodoId}/shares", new { userIds = Array.Empty<int>() })).StatusCode);
        Assert.Empty(await owner.GetFromJsonAsync<List<ShareItem>>($"/api/todos/{TodoId}/shares") ?? []);
        Assert.Empty((await shared.GetFromJsonAsync<PagedTodos>("/api/todos/shared"))!.Items);
        Assert.Equal(HttpStatusCode.NotFound, (await shared.GetAsync($"/api/todos/{TodoId}/notes")).StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal("Shared note", (await db.TodoNotes.AsNoTracking().SingleAsync()).Content);
        Assert.Equal("Updated after note", (await db.Todos.AsNoTracking().SingleAsync(todo => todo.Id == TodoId)).Description);
    }

    [Fact]
    public async Task UnrelatedUserCannotAccessNotes_AndReadPermissionIsRequired()
    {
        await GrantPermissionAsync(TestUsers.UserBId, PermissionCodes.TodosReadShared);
        await GrantPermissionAsync(TestUsers.UserAId, PermissionCodes.TodosShare);
        using var unrelated = _factory.CreateAuthenticatedClient(TestUsers.UserBId);
        using var owner = _factory.CreateAuthenticatedClient(TestUsers.UserAId);

        Assert.Empty((await unrelated.GetFromJsonAsync<PagedTodos>("/api/todos/shared"))!.Items);
        Assert.Equal(HttpStatusCode.Forbidden, (await unrelated.GetAsync($"/api/todos/{TodoId}/shares")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await unrelated.GetAsync($"/api/todos/{TodoId}/notes")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await unrelated.PostAsJsonAsync($"/api/todos/{TodoId}/notes", new { content = "No access" })).StatusCode);

        Assert.Equal(HttpStatusCode.OK,
            (await owner.PutAsJsonAsync($"/api/todos/{TodoId}/shares", new { userIds = new[] { TestUsers.UserBId } })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await unrelated.PostAsJsonAsync($"/api/todos/{TodoId}/notes", new { content = "Missing create permission" })).StatusCode);

        using var noPermission = _factory.CreateAuthenticatedClient(TestUsers.UserAId);
        Assert.Equal(HttpStatusCode.Forbidden, (await noPermission.GetAsync("/api/todos/shared")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await noPermission.GetAsync($"/api/todos/{TodoId}/shares")).StatusCode);
    }

    [Fact]
    public async Task ShareReplacementValidatesWholeRecipientSetBeforeChangingShares()
    {
        await GrantPermissionAsync(TestUsers.UserAId, PermissionCodes.TodosShare);
        using var owner = _factory.CreateAuthenticatedClient(TestUsers.UserAId);
        Assert.Equal(HttpStatusCode.OK, (await owner.PutAsJsonAsync($"/api/todos/{TodoId}/shares", new { userIds = new[] { TestUsers.UserBId } })).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest,
            (await owner.PutAsJsonAsync($"/api/todos/{TodoId}/shares", new { userIds = (int[]?)null })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await owner.PutAsJsonAsync($"/api/todos/{TodoId}/shares", new { userIds = new[] { TestUsers.UserBId, 99999 } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await owner.PutAsJsonAsync($"/api/todos/{TodoId}/shares", new { userIds = new[] { TestUsers.UserAId } })).StatusCode);

        var shares = await owner.GetFromJsonAsync<List<ShareItem>>($"/api/todos/{TodoId}/shares");
        Assert.Equal(TestUsers.UserBId, Assert.Single(shares!).SharedWithUserId);
    }

    [Fact]
    public async Task NoteContentMustBeNonWhitespaceAndAtMostTwoThousandCharacters()
    {
        await GrantPermissionAsync(TestUsers.UserAId, PermissionCodes.TodosNotesCreate);
        using var owner = _factory.CreateAuthenticatedClient(TestUsers.UserAId);

        Assert.Equal(HttpStatusCode.BadRequest,
            (await owner.PostAsJsonAsync($"/api/todos/{TodoId}/notes", new { content = "  \t" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await owner.PostAsJsonAsync($"/api/todos/{TodoId}/notes", new { content = new string('x', 2001) })).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await owner.PostAsJsonAsync($"/api/todos/{TodoId}/notes", new { content = new string('x', 2000) })).StatusCode);
    }

    private async Task GrantPermissionAsync(int userId, string permissionCode)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var roleId = await db.Roles.MaxAsync(role => role.Id) + 1;
        var permissionId = await db.Permissions.Where(permission => permission.Code == permissionCode)
            .Select(permission => permission.Id).SingleAsync();
        db.Roles.Add(new Role { Id = roleId, Name = $"TestRole{roleId}", Description = "Test permission" });
        db.UserRoles.Add(new UserRole { UserId = userId, RoleId = roleId });
        db.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = permissionId });
        await db.SaveChangesAsync();
    }

    private sealed class PagedTodos
    {
        public List<TodoItem> Items { get; set; } = [];
    }

    private sealed class TodoItem
    {
        public int Id { get; set; }
        public string OwnerDisplayName { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    private sealed class ShareItem
    {
        public int SharedWithUserId { get; set; }
    }

    private sealed class NoteItem
    {
        public string DisplayName { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }
}
