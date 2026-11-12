using BE_ZSM.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace BE_ZSM.Tests;

public sealed class AdminRecoveryMigrationTests
{
    [Fact]
    public void RecoveryRoleMigration_AbortsBeforeChangingAnExistingNameCollision()
    {
        var migrationBuilder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        new TestableAddAdminRecoveryMigration().Apply(migrationBuilder);

        var sql = Assert.Single(migrationBuilder.Operations.OfType<SqlOperation>()).Sql;
        var collisionCheck = sql.IndexOf("IF EXISTS (SELECT 1 FROM [Roles]", StringComparison.Ordinal);
        var abort = sql.IndexOf("THROW 51001", StringComparison.Ordinal);
        var roleInsert = sql.IndexOf("INSERT INTO [Roles]", StringComparison.Ordinal);
        var assignmentInsert = sql.IndexOf("INSERT INTO [RolePermissions]", StringComparison.Ordinal);

        Assert.True(collisionCheck >= 0 && collisionCheck < abort);
        Assert.True(abort < roleInsert);
        Assert.True(roleInsert < assignmentInsert);
        Assert.DoesNotContain("DELETE assignment", sql, StringComparison.Ordinal);
        Assert.Contains("Resolve the collision manually", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void RecoveryPermissionsMigration_ChangesOnlyTheRecoveryRoleAssignments()
    {
        var migrationBuilder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        new TestableUpdateAdminRecoveryPermissionsMigration().Apply(migrationBuilder);

        var sql = Assert.Single(migrationBuilder.Operations.OfType<SqlOperation>()).Sql;

        Assert.Contains("RBAC_RECOVERY_ADMIN", sql, StringComparison.Ordinal);
        Assert.Contains("roles.read", sql, StringComparison.Ordinal);
        Assert.Contains("permissions.read", sql, StringComparison.Ordinal);
        Assert.Contains("roles.manage-permissions", sql, StringComparison.Ordinal);
        Assert.Contains("users.manage-roles", sql, StringComparison.Ordinal);
        Assert.Contains("assignment.[RoleId] = @RecoveryRoleId", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("DELETE FROM [UserRoles]", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE FROM [Roles]", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LegacyRecoveryMigrationReportsAssignedUsersAndDoesNotTransferOrDeleteData()
    {
        var migrationBuilder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        new TestableMigrateLegacyRecoveryRoleMigration().Apply(migrationBuilder);

        var sql = Assert.Single(migrationBuilder.Operations.OfType<SqlOperation>()).Sql;
        var roleLookup = sql.IndexOf("RBAC_RECOVERY_ADMIN", StringComparison.Ordinal);
        var assignedUsers = sql.IndexOf("STRING_AGG", StringComparison.Ordinal);
        var abort = sql.IndexOf("THROW 51005", StringComparison.Ordinal);

        Assert.True(roleLookup >= 0 && roleLookup < assignedUsers);
        Assert.True(assignedUsers < abort);
        Assert.Contains("[UserRoles]", sql, StringComparison.Ordinal);
        Assert.Contains("[Users]", sql, StringComparison.Ordinal);
        Assert.Contains("[RoleId] = @LegacyRoleId", sql, StringComparison.Ordinal);
        Assert.Contains("IDs:", sql, StringComparison.Ordinal);
        Assert.Contains("No role or assignment data was changed", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("INSERT INTO [UserRoles]", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE FROM [UserRoles]", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE FROM [Roles]", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TodoPermissionMigrationSeedsReadAllWithoutGrantingItToAnyRole()
    {
        var migrationBuilder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        new TestableSeedTodoAuthorizationPermissionsMigration().Apply(migrationBuilder);

        var sql = Assert.Single(migrationBuilder.Operations.OfType<SqlOperation>()).Sql;

        Assert.Contains("todos.read-all", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("N'todos.read',", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("INSERT INTO [RolePermissions]", sql, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO [Permissions]", sql, StringComparison.Ordinal);
    }

    private sealed class TestableAddAdminRecoveryMigration : AddAdminRecovery
    {
        public void Apply(MigrationBuilder migrationBuilder) => Up(migrationBuilder);
    }

    private sealed class TestableUpdateAdminRecoveryPermissionsMigration : UpdateAdminRecoveryPermissions
    {
        public void Apply(MigrationBuilder migrationBuilder) => Up(migrationBuilder);
    }

    private sealed class TestableMigrateLegacyRecoveryRoleMigration : MigrateLegacyRecoveryRoleSafely
    {
        public void Apply(MigrationBuilder migrationBuilder) => Up(migrationBuilder);
    }

    private sealed class TestableSeedTodoAuthorizationPermissionsMigration : SeedTodoAuthorizationPermissions
    {
        public void Apply(MigrationBuilder migrationBuilder) => Up(migrationBuilder);
    }
}
