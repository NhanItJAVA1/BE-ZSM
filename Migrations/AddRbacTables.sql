BEGIN TRANSACTION;
CREATE TABLE [Permissions] (
    [Id] int NOT NULL IDENTITY,
    [Code] nvarchar(100) NOT NULL,
    [Description] nvarchar(500) NOT NULL,
    CONSTRAINT [PK_Permissions] PRIMARY KEY ([Id])
);

CREATE TABLE [UserRoles] (
    [UserId] int NOT NULL,
    [RoleId] int NOT NULL,
    CONSTRAINT [PK_UserRoles] PRIMARY KEY ([UserId], [RoleId]),
    CONSTRAINT [FK_UserRoles_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]),
    CONSTRAINT [FK_UserRoles_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [RolePermissions] (
    [RoleId] int NOT NULL,
    [PermissionId] int NOT NULL,
    CONSTRAINT [PK_RolePermissions] PRIMARY KEY ([RoleId], [PermissionId]),
    CONSTRAINT [FK_RolePermissions_Permissions_PermissionId] FOREIGN KEY ([PermissionId]) REFERENCES [Permissions] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_RolePermissions_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id])
);

INSERT INTO [UserRoles] ([UserId], [RoleId]) SELECT [Id], [RoleId] FROM [Users];

IF EXISTS (    SELECT 1 FROM [Users] AS [u]     LEFT JOIN [UserRoles] AS [ur]         ON [ur].[UserId] = [u].[Id] AND [ur].[RoleId] = [u].[RoleId]     WHERE [ur].[UserId] IS NULL) BEGIN     THROW 51000, 'RBAC UserRoles backfill validation failed.', 1; END;

CREATE UNIQUE INDEX [IX_Permissions_Code] ON [Permissions] ([Code]);

CREATE INDEX [IX_RolePermissions_PermissionId] ON [RolePermissions] ([PermissionId]);

CREATE INDEX [IX_UserRoles_RoleId] ON [UserRoles] ([RoleId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261108115808_AddRbacTables', N'10.0.10');

COMMIT;
GO

