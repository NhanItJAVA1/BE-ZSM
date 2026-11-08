using BE_ZSM.Authorization;
using BE_ZSM.Contexts;
using BE_ZSM.Entities;
using BE_ZSM.Entities.Rbac;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;

namespace BE_ZSM.Tests;

public sealed class RbacManagementTests : IAsyncLifetime, IDisposable
{
    private readonly TestApiFactory _factory = new();

    public async Task InitializeAsync() => await _factory.SeedUsersAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task RegularUser_CannotManageRolesOrPermissions()
    {
        using var client = _factory.CreateAuthenticatedClient(TestUsers.UserAId);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/roles")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/permissions")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/roles/1/permissions")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync(
            $"/api/users/{TestUsers.UserAId}/roles", new { roleIds = new[] { 2 } })).StatusCode);
    }

    [Fact]
    public async Task AdminPermission_AllowsRoleCreateAndRead()
    {
        await GrantAdminRoleToUserAAsync();
        using var client = _factory.CreateAuthenticatedClientWithRoles(TestUsers.UserAId, "User");

        var create = await client.PostAsJsonAsync("/api/roles", new { name = "Auditor", description = "Read only" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<RoleDto>();
        Assert.NotNull(created);
        Assert.True(created.Id > 2);

        var list = await client.GetFromJsonAsync<List<RoleDto>>("/api/roles");
        Assert.Contains(list!, role => role.Name == "Auditor");
        var permissions = await client.GetFromJsonAsync<List<PermissionDto>>("/api/permissions");
        Assert.Contains(permissions!, permission => permission.Code == PermissionCodes.UsersManageRoles);
    }

    [Fact]
    public async Task RoleName_IsTrimmedAndCaseInsensitiveDuplicatesAreRejected()
    {
        await GrantAdminRoleToUserAAsync();
        using var client = _factory.CreateAuthenticatedClient(TestUsers.UserAId);

        var created = await client.PostAsJsonAsync("/api/roles", new { name = " Auditor ", description = "" });
        var duplicate = await client.PostAsJsonAsync("/api/roles", new { name = "auditor", description = "" });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task RoleUpdate_ChangesNonDefaultRoleAndRejectsNameCollision()
    {
        await GrantAdminRoleToUserAAsync();
        using var client = _factory.CreateAuthenticatedClient(TestUsers.UserAId);
        var create = await client.PostAsJsonAsync("/api/roles", new { name = "Editor", description = "Old" });
        var role = await create.Content.ReadFromJsonAsync<RoleDto>();
        Assert.NotNull(role);

        var update = await client.PutAsJsonAsync($"/api/roles/{role.Id}", new { name = " Editor Plus ", description = " Updated " });
        var collision = await client.PutAsJsonAsync($"/api/roles/{role.Id}", new { name = "admin", description = "" });
        var delete = await client.DeleteAsync($"/api/roles/{role.Id}");

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updatedRole = await update.Content.ReadFromJsonAsync<RoleDto>();
        Assert.Equal("Editor Plus", updatedRole!.Name);
        Assert.Equal(HttpStatusCode.Conflict, collision.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }

    [Fact]
    public async Task RoleDelete_ProtectsDefaultAndRolesInUse()
    {
        await GrantAdminRoleToUserAAsync();
        using var client = _factory.CreateAuthenticatedClient(TestUsers.UserAId);
        var create = await client.PostAsJsonAsync("/api/roles", new { name = "Assigned", description = "" });
        var role = await create.Content.ReadFromJsonAsync<RoleDto>();
        Assert.NotNull(role);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.UserRoles.Add(new UserRole { UserId = TestUsers.UserBId, RoleId = role.Id });
            await db.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync("/api/roles/1")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/roles/{role.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync("/api/roles/99999")).StatusCode);
    }

    [Fact]
    public async Task RoleWithPermissionGrant_CannotBeDeleted()
    {
        await GrantAdminRoleToUserAAsync();
        using var client = _factory.CreateAuthenticatedClient(TestUsers.UserAId);
        var create = await client.PostAsJsonAsync("/api/roles", new { name = "Permissioned", description = "" });
        var role = await create.Content.ReadFromJsonAsync<RoleDto>();
        Assert.NotNull(role);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var permissionId = await db.Permissions.Where(permission => permission.Code == PermissionCodes.TodoRead)
                .Select(permission => permission.Id).SingleAsync();
            db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permissionId });
            await db.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/roles/{role.Id}")).StatusCode);
    }

    [Fact]
    public async Task DefaultRoleName_CannotBeChanged()
    {
        await GrantAdminRoleToUserAAsync();
        using var client = _factory.CreateAuthenticatedClient(TestUsers.UserAId);

        var response = await client.PutAsJsonAsync("/api/roles/2", new { name = "SuperAdmin", description = "Changed" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task RolePermissionReplacement_CannotRemoveBothManagementPermissionsFromLastAdmin()
    {
        await GrantAdminRoleToUserAAsync();
        using var client = _factory.CreateAuthenticatedClient(TestUsers.UserAId);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var retainedPermissionIds = await db.Permissions
            .Where(permission => permission.Code != PermissionCodes.RolesManagePermissions
                && permission.Code != PermissionCodes.UsersManageRoles)
            .Select(permission => permission.Id)
            .ToArrayAsync();

        var response = await client.PutAsJsonAsync("/api/roles/2/permissions", new { permissionIds = retainedPermissionIds });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.True(await db.RolePermissions.AnyAsync(item => item.RoleId == 2
            && item.Permission.Code == PermissionCodes.RolesManagePermissions));
    }

    [Fact]
    public async Task RolePermissionReplacement_ValidatesIdsAddsAndRevokes()
    {
        await GrantAdminRoleToUserAAsync();
        using var client = _factory.CreateAuthenticatedClient(TestUsers.UserAId);
        var create = await client.PostAsJsonAsync("/api/roles", new { name = "Auditor", description = "" });
        var role = await create.Content.ReadFromJsonAsync<RoleDto>();
        Assert.NotNull(role);
        int[] permissionIds;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            permissionIds = await db.Permissions.Where(permission =>
                    permission.Code == PermissionCodes.TodoRead || permission.Code == PermissionCodes.TodoWrite)
                .Select(permission => permission.Id)
                .ToArrayAsync();
        }

        var invalid = await client.PutAsJsonAsync($"/api/roles/{role.Id}/permissions", new { permissionIds = new[] { 999999 } });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        int usersManageRolesPermissionId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            usersManageRolesPermissionId = await db.Permissions
                .Where(permission => permission.Code == PermissionCodes.UsersManageRoles)
                .Select(permission => permission.Id)
                .SingleAsync();
        }
        var grantUserRole = await client.PutAsJsonAsync("/api/roles/1/permissions", new
        {
            permissionIds = new[] { usersManageRolesPermissionId }
        });
        Assert.Equal(HttpStatusCode.Conflict, grantUserRole.StatusCode);

        var assign = await client.PutAsJsonAsync($"/api/roles/{role.Id}/permissions", new { permissionIds });
        Assert.Equal(HttpStatusCode.OK, assign.StatusCode);
        var repeatedAssign = await client.PutAsJsonAsync($"/api/roles/{role.Id}/permissions", new { permissionIds });
        Assert.Equal(HttpStatusCode.OK, repeatedAssign.StatusCode);
        var current = await client.GetFromJsonAsync<List<PermissionDto>>($"/api/roles/{role.Id}/permissions");
        Assert.Equal(2, current!.Count);

        var revoke = await client.PutAsJsonAsync($"/api/roles/{role.Id}/permissions", new { permissionIds = Array.Empty<int>() });
        Assert.Equal(HttpStatusCode.OK, revoke.StatusCode);
        current = await client.GetFromJsonAsync<List<PermissionDto>>($"/api/roles/{role.Id}/permissions");
        Assert.Empty(current!);
    }

    [Fact]
    public async Task RolePermissionReplacement_RejectsDuplicateIdsAndUnknownRole()
    {
        await GrantAdminRoleToUserAAsync();
        using var client = _factory.CreateAuthenticatedClient(TestUsers.UserAId);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var permissionId = await db.Permissions.Where(permission => permission.Code == PermissionCodes.TodoRead)
            .Select(permission => permission.Id).SingleAsync();

        var duplicate = await client.PutAsJsonAsync("/api/roles/1/permissions", new { permissionIds = new[] { permissionId, permissionId } });
        var unknownRole = await client.GetAsync("/api/roles/99999/permissions");

        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknownRole.StatusCode);
    }

    [Fact]
    public async Task UserRoleReplacement_PreservesLegacyRoleAndBlocksLastAdminRemoval()
    {
        await GrantAdminRoleToUserAAsync();
        using var client = _factory.CreateAuthenticatedClientWithRoles(TestUsers.UserAId, "User");

        var replace = await client.PutAsJsonAsync($"/api/users/{TestUsers.UserAId}/roles", new { roleIds = new[] { 1, 2 } });
        Assert.Equal(HttpStatusCode.OK, replace.StatusCode);
        var roles = await client.GetFromJsonAsync<List<RoleDto>>($"/api/users/{TestUsers.UserAId}/roles");
        Assert.Equal(2, roles!.Count);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync(item => item.Id == TestUsers.UserAId);
            Assert.Equal(1, user.RoleId);
        }

        var removeAdmin = await client.PutAsJsonAsync($"/api/users/{TestUsers.UserAId}/roles", new { roleIds = new[] { 1 } });
        var deleteLastAdmin = await client.DeleteAsync($"/api/users/{TestUsers.UserAId}");

        Assert.Equal(HttpStatusCode.Conflict, removeAdmin.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, deleteLastAdmin.StatusCode);
    }

    [Fact]
    public async Task UserRoleReplacement_ValidatesUserRolesAndRequiresAtLeastOne()
    {
        await GrantAdminRoleToUserAAsync();
        using var client = _factory.CreateAuthenticatedClient(TestUsers.UserAId);

        var empty = await client.PutAsJsonAsync($"/api/users/{TestUsers.UserBId}/roles", new { roleIds = Array.Empty<int>() });
        var missingRole = await client.PutAsJsonAsync($"/api/users/{TestUsers.UserBId}/roles", new { roleIds = new[] { 123456 } });
        var missingUser = await client.GetAsync("/api/users/99999/roles");

        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, missingRole.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingUser.StatusCode);
    }

    private async Task GrantAdminRoleToUserAAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (!await db.UserRoles.AnyAsync(item => item.UserId == TestUsers.UserAId && item.RoleId == 2))
        {
            db.UserRoles.Add(new UserRole { UserId = TestUsers.UserAId, RoleId = 2 });
            await db.SaveChangesAsync();
        }
    }

    private sealed class RoleDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private sealed class PermissionDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
    }
}
