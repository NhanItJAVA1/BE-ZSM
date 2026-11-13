
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BE_ZSM.Migrations
{
    public partial class UseIdentityForRoleId : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
            => RebuildRoles(migrationBuilder, useIdentity: true);

        protected override void Down(MigrationBuilder migrationBuilder)
            => RebuildRoles(migrationBuilder, useIdentity: false);

        private static void RebuildRoles(
            MigrationBuilder migrationBuilder,
            bool useIdentity)
        {
            var identity = useIdentity ? "IDENTITY(1,1)" : "";

            migrationBuilder.Sql($"""
                CREATE TABLE [dbo].[Roles_New]
                (
                    [Id] INT {identity} NOT NULL,
                    [Name] NVARCHAR(50) NOT NULL,
                    [Description] NVARCHAR(MAX) NOT NULL,
                    [NameNormalized] AS (UPPER([Name])) PERSISTED
                );
                """);

            if (useIdentity)
                migrationBuilder.Sql(
                    "SET IDENTITY_INSERT [dbo].[Roles_New] ON;");

            migrationBuilder.Sql("""
                INSERT INTO [dbo].[Roles_New]
                    ([Id], [Name], [Description])
                SELECT [Id], [Name], [Description]
                FROM [dbo].[Roles];
                """);

            if (useIdentity)
                migrationBuilder.Sql(
                    "SET IDENTITY_INSERT [dbo].[Roles_New] OFF;");

            migrationBuilder.Sql("""
                ALTER TABLE [dbo].[UserRoles]
                    DROP CONSTRAINT [FK_UserRoles_Roles_RoleId];

                ALTER TABLE [dbo].[RolePermissions]
                    DROP CONSTRAINT [FK_RolePermissions_Roles_RoleId];

                ALTER TABLE [dbo].[Users]
                    DROP CONSTRAINT [FK_Users_Roles_RoleId];

                DROP TABLE [dbo].[Roles];

                EXEC sp_rename
                    N'dbo.Roles_New', N'Roles';

                ALTER TABLE [dbo].[Roles]
                    ADD CONSTRAINT [PK_Roles]
                    PRIMARY KEY CLUSTERED ([Id]);

                CREATE UNIQUE INDEX [IX_Roles_Name]
                    ON [dbo].[Roles] ([Name]);

                CREATE UNIQUE INDEX [IX_Roles_NameNormalized]
                    ON [dbo].[Roles] ([NameNormalized]);

                ALTER TABLE [dbo].[UserRoles] WITH CHECK
                    ADD CONSTRAINT [FK_UserRoles_Roles_RoleId]
                    FOREIGN KEY ([RoleId])
                    REFERENCES [dbo].[Roles] ([Id])
                    ON DELETE NO ACTION;

                ALTER TABLE [dbo].[RolePermissions] WITH CHECK
                    ADD CONSTRAINT [FK_RolePermissions_Roles_RoleId]
                    FOREIGN KEY ([RoleId])
                    REFERENCES [dbo].[Roles] ([Id])
                    ON DELETE NO ACTION;

                ALTER TABLE [dbo].[Users] WITH CHECK
                    ADD CONSTRAINT [FK_Users_Roles_RoleId]
                    FOREIGN KEY ([RoleId])
                    REFERENCES [dbo].[Roles] ([Id])
                    ON DELETE CASCADE;
                """);
        }
    }
}
