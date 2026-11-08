BEGIN TRANSACTION;
CREATE TABLE [RbacPermissionSeedHistory] (
    [Id] int NOT NULL IDENTITY,
    [EntryType] tinyint NOT NULL,
    [PermissionCode] nvarchar(100) NOT NULL,
    [RoleId] int NULL,
    [PermissionId] int NULL,
    CONSTRAINT [PK_RbacPermissionSeedHistory] PRIMARY KEY ([Id])
);


IF NOT EXISTS (SELECT 1 FROM [Roles] WHERE [Name] = N'User')
    THROW 51100, 'Required User role is missing; permission seeding aborted.', 1;
IF NOT EXISTS (SELECT 1 FROM [Roles] WHERE [Name] = N'Admin')
    THROW 51101, 'Required Admin role is missing; permission seeding aborted.', 1;

DECLARE @NewPermissions TABLE ([Id] int NOT NULL, [Code] nvarchar(100) NOT NULL);
INSERT INTO [Permissions] ([Code], [Description])
OUTPUT inserted.[Id], inserted.[Code] INTO @NewPermissions ([Id], [Code])
SELECT seed.[Code], seed.[Description]
FROM (VALUES
    (N'todo.read', N'Read todos'),
    (N'todo.write', N'Create, update, and delete todos'),
    (N'todo-category.read', N'Read todo categories'),
    (N'todo-category.create', N'Create todo categories'),
    (N'todo-category.update', N'Update todo categories'),
    (N'todo-category.delete', N'Delete todo categories'),
    (N'catalog.upload', N'Create catalog upload URLs'),
    (N'users.read', N'Read users'),
    (N'users.update', N'Update users'),
    (N'users.delete', N'Delete users')
) AS seed([Code], [Description])
WHERE NOT EXISTS (SELECT 1 FROM [Permissions] p WHERE p.[Code] = seed.[Code]);

INSERT INTO [RbacPermissionSeedHistory] ([EntryType], [PermissionCode], [PermissionId])
SELECT 1, [Code], [Id] FROM @NewPermissions;

DECLARE @SeedGrants TABLE ([RoleName] nvarchar(50) NOT NULL, [Code] nvarchar(100) NOT NULL);
INSERT INTO @SeedGrants ([RoleName], [Code]) VALUES
    (N'User', N'todo.read'), (N'User', N'todo.write'),
    (N'User', N'todo-category.read'), (N'User', N'todo-category.create'),
    (N'User', N'todo-category.update'), (N'User', N'todo-category.delete'),
    (N'Admin', N'todo.read'), (N'Admin', N'todo.write'),
    (N'Admin', N'todo-category.read'), (N'Admin', N'todo-category.create'),
    (N'Admin', N'todo-category.update'), (N'Admin', N'todo-category.delete'),
    (N'Admin', N'catalog.upload'), (N'Admin', N'users.read'),
    (N'Admin', N'users.update'), (N'Admin', N'users.delete');

DECLARE @NewGrants TABLE ([RoleId] int NOT NULL, [PermissionId] int NOT NULL);
INSERT INTO [RolePermissions] ([RoleId], [PermissionId])
OUTPUT inserted.[RoleId], inserted.[PermissionId]
    INTO @NewGrants ([RoleId], [PermissionId])
SELECT r.[Id], p.[Id]
FROM @SeedGrants seed
INNER JOIN [Roles] r ON r.[Name] = seed.[RoleName]
INNER JOIN [Permissions] p ON p.[Code] = seed.[Code]
WHERE NOT EXISTS (
    SELECT 1 FROM [RolePermissions] existing
    WHERE existing.[RoleId] = r.[Id] AND existing.[PermissionId] = p.[Id]);

INSERT INTO [RbacPermissionSeedHistory] ([EntryType], [PermissionCode], [RoleId], [PermissionId])
SELECT 2, p.[Code], grantRow.[RoleId], grantRow.[PermissionId]
FROM @NewGrants grantRow
INNER JOIN [Permissions] p ON p.[Id] = grantRow.[PermissionId];

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261108142114_SeedDynamicPermissions', N'10.0.10');

COMMIT;
GO

