using System.Net;
using System.Net.Http.Json;
using BE_ZSM.Authorization;
using BE_ZSM.Contexts;
using BE_ZSM.Entities;
using BE_ZSM.Entities.Rbac;
using BE_ZSM.Services.AdminRecovery;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace BE_ZSM.Tests;

public sealed class AdminRecoveryTests : IAsyncLifetime, IDisposable
{
    private readonly TestApiFactory _factory = new();
    private const int OwnerId = 1003;

    public async Task InitializeAsync()
    {
        await _factory.SeedUsersAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Users.Add(new User
        {
            Id = OwnerId, Username = "system-owner", Email = "owner@example.test", DisplayName = "System Owner",
            RoleId = 1, IsSystemOwner = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;
    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Restore_RequiresAuthenticationAndOwner()
    {
        using var anonymous = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/admin-recovery/restore", Request(TestUsers.UserAId))).StatusCode);
        using var nonOwner = _factory.CreateAuthenticatedClient(TestUsers.UserAId);
        Assert.Equal(HttpStatusCode.Forbidden, (await nonOwner.PostAsJsonAsync("/api/admin-recovery/restore", Request(TestUsers.UserBId))).StatusCode);
    }

    [Fact]
    public async Task SearchUsers_RequiresAuthenticationAndSystemOwner()
    {
        using var anonymous = _factory.CreateClient();
        using var nonOwner = _factory.CreateAuthenticatedClient(TestUsers.UserAId);

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/admin-recovery/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await nonOwner.GetAsync("/api/admin-recovery/users")).StatusCode);
    }

    [Fact]
    public async Task SystemOwnerCanSearchByDisplayNameEmailOrUserIdWithoutSensitiveFields()
    {
        using var owner = _factory.CreateAuthenticatedClient(OwnerId);

        var displayNameResponse = await owner.GetAsync("/api/admin-recovery/users?search=todo%20user%20a");
        var emailResponse = await owner.GetAsync("/api/admin-recovery/users?search=todo-user-b%40example.test");
        var idResponse = await owner.GetAsync($"/api/admin-recovery/users?search={TestUsers.UserAId}");

        Assert.Equal(HttpStatusCode.OK, displayNameResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, emailResponse.StatusCode);
        var displayNameJson = await displayNameResponse.Content.ReadFromJsonAsync<JsonElement>();
        var emailJson = await emailResponse.Content.ReadFromJsonAsync<JsonElement>();
        var idJson = await idResponse.Content.ReadFromJsonAsync<JsonElement>();
        var displayNameItem = displayNameJson.GetProperty("items").EnumerateArray().Single();
        Assert.Equal(TestUsers.UserAId, displayNameItem.GetProperty("id").GetInt32());
        Assert.Equal(TestUsers.UserBId, emailJson.GetProperty("items").EnumerateArray().Single().GetProperty("id").GetInt32());
        Assert.Equal(TestUsers.UserAId, idJson.GetProperty("items").EnumerateArray().Single().GetProperty("id").GetInt32());
        Assert.Equal(new[] { "displayName", "email", "id" },
            displayNameItem.EnumerateObject().Select(property => property.Name).OrderBy(name => name));
        Assert.DoesNotContain(displayNameJson.GetProperty("items").EnumerateArray(), item => item.GetProperty("id").GetInt32() == OwnerId);
    }

    [Fact]
    public async Task SearchUsers_PaginatesAndCapsPageSize()
    {
        using var owner = _factory.CreateAuthenticatedClient(OwnerId);

        var firstPageResponse = await owner.GetAsync("/api/admin-recovery/users?page=1&pageSize=1");
        var secondPageResponse = await owner.GetAsync("/api/admin-recovery/users?page=2&pageSize=1");
        var oversizedResponse = await owner.GetAsync("/api/admin-recovery/users?pageSize=500");
        var firstPage = await firstPageResponse.Content.ReadFromJsonAsync<JsonElement>();
        var secondPage = await secondPageResponse.Content.ReadFromJsonAsync<JsonElement>();
        var oversized = await oversizedResponse.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(1, firstPage.GetProperty("page").GetInt32());
        Assert.Equal(1, firstPage.GetProperty("pageSize").GetInt32());
        Assert.Equal(2, firstPage.GetProperty("totalItems").GetInt32());
        Assert.Equal(2, firstPage.GetProperty("totalPages").GetInt32());
        Assert.NotEqual(firstPage.GetProperty("items")[0].GetProperty("id").GetInt32(),
            secondPage.GetProperty("items")[0].GetProperty("id").GetInt32());
        Assert.Equal(100, oversized.GetProperty("pageSize").GetInt32());
        Assert.DoesNotContain(oversized.GetProperty("items").EnumerateArray(), item => item.GetProperty("id").GetInt32() == OwnerId);
    }

    [Fact]
    public async Task OwnerRestoresAdminMembershipWithoutRequiringPriorAdminAndKeepsExistingRolesAndPermissions()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Roles.Add(new Role { Id = 3, Name = "Existing", Description = "Existing role" });
        db.UserRoles.Add(new UserRole { UserId = TestUsers.UserAId, RoleId = 3 });
        await db.SaveChangesAsync();
        using var owner = _factory.CreateAuthenticatedClient(OwnerId);

        var response = await owner.PostAsJsonAsync("/api/admin-recovery/restore", Request(TestUsers.UserAId));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(new[] { 1, 2, 3 }, await db.UserRoles.Where(item => item.UserId == TestUsers.UserAId)
            .Select(item => item.RoleId).OrderBy(id => id).ToArrayAsync());
        var adminPermissionCodes = await db.RolePermissions.Where(item => item.RoleId == 2)
            .Select(item => item.Permission.Code).ToListAsync();
        Assert.All(PermissionCodes.AdminRecoveryPermissions, code => Assert.Contains(code, adminPermissionCodes));
        Assert.Contains(PermissionCodes.TodoRead, adminPermissionCodes);
    }

    [Fact]
    public async Task RecoveryRestoresFourAdminPermissionsAndPreservesExistingAdminPermissions()
    {
        using (var setupScope = _factory.Services.CreateScope())
        {
            var setupDb = setupScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var permissions = await setupDb.RolePermissions.Where(item => item.RoleId == 2
                && item.Permission.Code != PermissionCodes.TodoRead).ToListAsync();
            setupDb.RolePermissions.RemoveRange(permissions);
            await setupDb.SaveChangesAsync();
        }

        using var owner = _factory.CreateAuthenticatedClient(OwnerId);
        Assert.Equal(HttpStatusCode.NoContent,
            (await owner.PostAsJsonAsync("/api/admin-recovery/restore", Request(TestUsers.UserAId))).StatusCode);

        using var ordinaryRecoveredUser = _factory.CreateAuthenticatedClient(TestUsers.UserAId);
        Assert.Equal(HttpStatusCode.OK, (await ordinaryRecoveredUser.GetAsync("/api/roles")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ordinaryRecoveredUser.GetAsync("/api/permissions")).StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var permissionCodes = await db.RolePermissions.Where(item => item.RoleId == 2)
            .Select(item => item.Permission.Code).OrderBy(code => code).ToArrayAsync();
        Assert.Equal(new[]
        {
            PermissionCodes.PermissionsRead,
            PermissionCodes.RolesManagePermissions,
            PermissionCodes.RolesRead,
            PermissionCodes.TodoRead,
            PermissionCodes.UsersManageRoles
        }.OrderBy(code => code), permissionCodes);
        Assert.Equal(PermissionCodes.AdminRecoveryPermissions.Count + 1, permissionCodes.Length);
    }

    [Fact]
    public async Task OwnerCannotRestoreSelfOrMissingUser()
    {
        using var owner = _factory.CreateAuthenticatedClient(OwnerId);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync("/api/admin-recovery/restore", Request(OwnerId))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.PostAsJsonAsync("/api/admin-recovery/restore", Request(99999))).StatusCode);
    }

    [Fact]
    public async Task RepeatedAndConcurrentRequestsAreIdempotentAndAudited()
    {
        using var owner = _factory.CreateAuthenticatedClient(OwnerId);
        var first = await owner.PostAsJsonAsync("/api/admin-recovery/restore", Request(TestUsers.UserAId));
        var second = await owner.PostAsJsonAsync("/api/admin-recovery/restore", Request(TestUsers.UserAId));
        var simultaneous = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => owner.PostAsJsonAsync("/api/admin-recovery/restore", Request(TestUsers.UserAId))));
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        Assert.All(simultaneous, response => Assert.Equal(HttpStatusCode.NoContent, response.StatusCode));
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.UserRoles.CountAsync(item => item.UserId == TestUsers.UserAId && item.RoleId == 2));
        Assert.Equal(6, await db.AdminRecoveryAudits.CountAsync(item => item.TargetUserId == TestUsers.UserAId));
        Assert.Contains(await db.AdminRecoveryAudits.Select(item => item.Outcome).ToListAsync(), outcome => outcome == "already restored");
        Assert.All(await db.AdminRecoveryAudits.Select(item => item.Action).ToListAsync(),
            action => Assert.Equal("ADMIN_ROLE_ACCESS_RESTORED", action));
    }

    [Fact]
    public async Task SystemOwnerDoesNotBypassOrdinaryPermissions()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.UserRoles.Add(new UserRole { UserId = OwnerId, RoleId = 2 });
        await db.SaveChangesAsync();
        using var owner = _factory.CreateAuthenticatedClient(OwnerId);

        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync("/api/users")).StatusCode);
    }

    [Fact]
    public async Task LegacyRecoveryRoleCanBeSafelyCleanedUpButCannotBeReassignedOrRepurposed()
    {
        using var owner = _factory.CreateAuthenticatedClient(OwnerId);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.PostAsJsonAsync("/api/admin-recovery/restore", Request(TestUsers.UserAId))).StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        const string legacyRoleName = "RBAC_RECOVERY_ADMIN";
        var role = new Role { Id = 3, Name = legacyRoleName, Description = "Legacy recovery role" };
        db.Roles.Add(role);
        var rolePermissions = await db.Permissions.Where(item => PermissionCodes.AdminRecoveryPermissions.Contains(item.Code))
            .Select(item => item.Id).ToArrayAsync();
        db.RolePermissions.AddRange(rolePermissions.Select(permissionId => new RolePermission
        {
            RoleId = role.Id,
            PermissionId = permissionId
        }));
        db.UserRoles.Add(new UserRole { UserId = TestUsers.UserAId, RoleId = role.Id });
        await db.SaveChangesAsync();
        using var ordinaryAdmin = _factory.CreateAuthenticatedClient(TestUsers.UserAId);

        Assert.Equal(HttpStatusCode.Conflict, (await ordinaryAdmin.PutAsJsonAsync($"/api/roles/{role.Id}", new { name = "renamed", description = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await ordinaryAdmin.PostAsJsonAsync("/api/roles", new { name = legacyRoleName, description = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await ordinaryAdmin.DeleteAsync($"/api/roles/{role.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await ordinaryAdmin.PutAsJsonAsync($"/api/roles/{role.Id}/permissions", new { permissionIds = rolePermissions.Append(
            await db.Permissions.Where(item => item.Code == PermissionCodes.TodoRead).Select(item => item.Id).SingleAsync()).ToArray() })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await ordinaryAdmin.PutAsJsonAsync($"/api/users/{TestUsers.UserBId}/roles", new { roleIds = new[] { role.Id } })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await ordinaryAdmin.DeleteAsync($"/api/users/{OwnerId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ordinaryAdmin.PutAsJsonAsync($"/api/roles/{role.Id}/permissions", new { permissionIds = Array.Empty<int>() })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ordinaryAdmin.PutAsJsonAsync($"/api/users/{TestUsers.UserAId}/roles", new { roleIds = new[] { 1, 2 } })).StatusCode);
        Assert.False(await db.UserRoles.AnyAsync(item => item.UserId == TestUsers.UserBId && item.RoleId == role.Id));
        Assert.False(await db.UserRoles.AnyAsync(item => item.UserId == TestUsers.UserAId && item.RoleId == role.Id));
        Assert.Equal(HttpStatusCode.NoContent, (await ordinaryAdmin.DeleteAsync($"/api/roles/{role.Id}")).StatusCode);
        Assert.False(await db.Roles.AnyAsync(item => item.Id == role.Id));
    }

    [Fact]
    public async Task RestoreDoesNotCreateOrAssignLegacyRecoveryRoleAndPreservesExistingAssignments()
    {
        const int legacyRoleId = 3;
        using (var setupScope = _factory.Services.CreateScope())
        {
            var setupDb = setupScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var role = new Role { Id = legacyRoleId, Name = "RBAC_RECOVERY_ADMIN", Description = "Legacy recovery role" };
            setupDb.Roles.Add(role);
            var permissionIds = await setupDb.Permissions
                .Where(permission => PermissionCodes.AdminRecoveryPermissions.Contains(permission.Code))
                .Select(permission => permission.Id).ToArrayAsync();
            setupDb.RolePermissions.AddRange(permissionIds.Select(permissionId => new RolePermission
            {
                RoleId = legacyRoleId,
                PermissionId = permissionId
            }));
            setupDb.UserRoles.Add(new UserRole { UserId = TestUsers.UserBId, RoleId = legacyRoleId });
            await setupDb.SaveChangesAsync();
        }

        using var owner = _factory.CreateAuthenticatedClient(OwnerId);
        Assert.Equal(HttpStatusCode.NoContent,
            (await owner.PostAsJsonAsync("/api/admin-recovery/restore", Request(TestUsers.UserAId))).StatusCode);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await verifyDb.UserRoles.AnyAsync(item => item.UserId == TestUsers.UserAId && item.RoleId == 2));
        Assert.False(await verifyDb.UserRoles.AnyAsync(item => item.UserId == TestUsers.UserAId && item.RoleId == legacyRoleId));
        Assert.True(await verifyDb.UserRoles.AnyAsync(item => item.UserId == TestUsers.UserBId && item.RoleId == legacyRoleId));
        Assert.True(await verifyDb.Roles.AnyAsync(role => role.Id == legacyRoleId && role.Name == "RBAC_RECOVERY_ADMIN"));
    }

    [Fact]
    public async Task MissingDefaultAdminRoleFailsWithoutCreatingAnArbitraryRole()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.RolePermissions.RemoveRange(db.RolePermissions.Where(item => item.RoleId == 2));
            db.Roles.Remove(await db.Roles.SingleAsync(role => role.Id == 2));
            await db.SaveChangesAsync();
        }

        using var owner = _factory.CreateAuthenticatedClient(OwnerId);
        var response = await owner.PostAsJsonAsync("/api/admin-recovery/restore", Request(TestUsers.UserAId));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await verifyDb.Roles.AnyAsync(role => role.Name.ToUpper() == "ADMIN"));
        Assert.False(await verifyDb.UserRoles.AnyAsync(item => item.UserId == TestUsers.UserAId && item.RoleId == 2));
        Assert.Equal("failure", await verifyDb.AdminRecoveryAudits.Select(item => item.Outcome).SingleAsync());
    }

    private static object Request(int userId) => new { userId, reason = "Approved by manager - INC-2026-001" };
}
