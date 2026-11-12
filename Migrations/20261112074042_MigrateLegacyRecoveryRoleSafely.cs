using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BE_ZSM.Migrations
{
    /// <inheritdoc />
    public partial class MigrateLegacyRecoveryRoleSafely : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DECLARE @LegacyRoleId int = (SELECT [Id] FROM [Roles] WHERE [NameNormalized] = N'RBAC_RECOVERY_ADMIN');
IF @LegacyRoleId IS NOT NULL
BEGIN
    DECLARE @AssignedUserIds nvarchar(max);
    SELECT @AssignedUserIds = STRING_AGG(CONVERT(nvarchar(max), assigned.[UserId]), N',')
    FROM (
        SELECT [UserId] FROM [UserRoles] WHERE [RoleId] = @LegacyRoleId
        UNION
        SELECT [Id] AS [UserId] FROM [Users] WHERE [RoleId] = @LegacyRoleId
    ) AS assigned;

    IF @AssignedUserIds IS NOT NULL
    BEGIN
        DECLARE @MigrationError nvarchar(2048) =
            N'Legacy RBAC_RECOVERY_ADMIN has assigned users (IDs: '
            + LEFT(@AssignedUserIds, 1500)
            + N'). Review and explicitly transfer or retain each assignment, then remove the legacy memberships before retrying this migration. No role or assignment data was changed.';
        ;THROW 51005, @MigrationError, 1;
    END;

    PRINT N'RBAC_RECOVERY_ADMIN exists without user assignments and was retained unchanged for manual review.';
END;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"THROW 51006, N'MigrateLegacyRecoveryRoleSafely is forward-only; resolve legacy Recovery Role assignments manually.', 1;");
        }
    }
}
