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

public sealed class PermissionAuthorizationTests : IAsyncLifetime, IDisposable
{
    private readonly TestApiFactory _factory = new();

    public async Task InitializeAsync() => await _factory.SeedUsersAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task TodoRead_WithoutJwt_Returns401()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/todos/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TodoRead_AuthenticatedWithoutPermission_Returns403()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var permissionId = await db.Permissions.Where(x => x.Code == PermissionCodes.TodoRead)
            .Select(x => x.Id).SingleAsync();
        var membership = await db.RolePermissions.SingleAsync(x => x.RoleId == 1 && x.PermissionId == permissionId);
        db.RolePermissions.Remove(membership);
        await db.SaveChangesAsync();
        using var client = _factory.CreateAuthenticatedClient(TestUsers.UserAId);

        var response = await client.GetAsync("/api/todos/me");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UserWithoutMembership_Returns403()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.UserRoles.RemoveRange(db.UserRoles.Where(x => x.UserId == TestUsers.UserAId));
        await db.SaveChangesAsync();
        using var client = _factory.CreateAuthenticatedClient(TestUsers.UserAId);

        var response = await client.GetAsync("/api/todos/me");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task NonexistentUserWithForgedMembership_Returns403()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.UserRoles.Add(new UserRole { UserId = 9999, RoleId = 2 });
        await db.SaveChangesAsync();
        using var client = _factory.CreateAuthenticatedClientWithRoles(9999, "Admin");

        var response = await client.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TodoRead_WithPermission_Returns200()
    {
        using var client = _factory.CreateAuthenticatedClient(TestUsers.UserAId);

        var response = await client.GetAsync("/api/todos/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Permissions_AreUnionedAcrossUserRoles()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var permissionId = await db.Permissions.Where(x => x.Code == PermissionCodes.TodoRead)
            .Select(x => x.Id).SingleAsync();
        var userGrant = await db.RolePermissions.SingleAsync(x => x.RoleId == 1 && x.PermissionId == permissionId);
        db.RolePermissions.Remove(userGrant);
        db.Roles.Add(new Role { Id = 3, Name = "Auditor", Description = "Read-only role" });
        db.UserRoles.Add(new UserRole { UserId = TestUsers.UserAId, RoleId = 3 });
        db.RolePermissions.Add(new RolePermission { RoleId = 3, PermissionId = permissionId });
        await db.SaveChangesAsync();
        using var client = _factory.CreateAuthenticatedClient(TestUsers.UserAId);

        var response = await client.GetAsync("/api/todos/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RemovingRoleMembership_TakesEffectOnNextRequest()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Roles.Add(new Role { Id = 3, Name = "Temporary", Description = "Temporary role" });
        db.UserRoles.Add(new UserRole { UserId = TestUsers.UserAId, RoleId = 3 });
        var permissionId = await db.Permissions.Where(x => x.Code == PermissionCodes.TodoRead)
            .Select(x => x.Id).SingleAsync();
        db.RolePermissions.Remove(await db.RolePermissions.SingleAsync(x => x.RoleId == 1 && x.PermissionId == permissionId));
        db.RolePermissions.Add(new RolePermission { RoleId = 3, PermissionId = permissionId });
        await db.SaveChangesAsync();
        using var client = _factory.CreateAuthenticatedClient(TestUsers.UserAId);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/todos/me")).StatusCode);

        db.UserRoles.Remove(await db.UserRoles.SingleAsync(x => x.UserId == TestUsers.UserAId && x.RoleId == 3));
        await db.SaveChangesAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/todos/me")).StatusCode);
    }

    [Fact]
    public async Task RemovingRolePermission_TakesEffectOnNextRequest()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var permissionId = await db.Permissions.Where(x => x.Code == PermissionCodes.TodoRead)
            .Select(x => x.Id).SingleAsync();
        using var client = _factory.CreateAuthenticatedClient(TestUsers.UserAId);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/todos/me")).StatusCode);

        db.RolePermissions.Remove(await db.RolePermissions.SingleAsync(x => x.RoleId == 1 && x.PermissionId == permissionId));
        await db.SaveChangesAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/todos/me")).StatusCode);
    }

    [Fact]
    public async Task AdminJwtClaimWithoutDatabasePermission_Returns403()
    {
        using var client = _factory.CreateAuthenticatedClientWithRoles(TestUsers.UserAId, "Admin");

        var response = await client.PostAsJsonAsync("/api/catalog/image-upload-url", new { fileName = "test.png" });
        var usersResponse = await client.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, usersResponse.StatusCode);
    }

    [Fact]
    public async Task DatabaseAdminPermission_AllowsUsersRead()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.UserRoles.Add(new UserRole { UserId = TestUsers.UserAId, RoleId = 2 });
        await db.SaveChangesAsync();
        using var client = _factory.CreateAuthenticatedClientWithRoles(TestUsers.UserAId, "User");

        var response = await client.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TodoWritePermission_DoesNotAllowUpdatingAnotherUsersTodo()
    {
        const int foreignTodoId = 9001;
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Todos.Add(new Todo
        {
            Id = foreignTodoId,
            UserId = TestUsers.UserBId,
            Title = "Owned by user B",
            Status = TodoStatus.Todo,
            Priority = TodoPriority.Medium,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        using var client = _factory.CreateAuthenticatedClient(TestUsers.UserAId);

        var response = await client.PutAsJsonAsync("/api/todos/batch", new[]
        {
            new { id = foreignTodoId, title = "Changed by user A", priority = "High" }
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var unchanged = await db.Todos.AsNoTracking().SingleAsync(x => x.Id == foreignTodoId);
        Assert.Equal(TestUsers.UserBId, unchanged.UserId);
        Assert.Equal("Owned by user B", unchanged.Title);
    }

    [Fact]
    public async Task TodoWritePermission_CannotAttachAnotherUsersCategory()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var foreignCategory = new TodoCategory
        {
            UserId = TestUsers.UserBId,
            Name = "Private category"
        };
        db.TodoCategories.Add(foreignCategory);
        await db.SaveChangesAsync();
        using var client = _factory.CreateAuthenticatedClient(TestUsers.UserAId);

        var response = await client.PutAsJsonAsync("/api/todos/batch", new[]
        {
            new { title = "New todo", categoryId = foreignCategory.Id }
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(await db.Todos.AsNoTracking().ToListAsync());
    }
}
