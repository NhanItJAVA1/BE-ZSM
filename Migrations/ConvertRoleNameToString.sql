BEGIN TRANSACTION;

IF EXISTS (SELECT 1 FROM [Roles] WHERE [Name] NOT IN (0, 1))
    THROW 51001, 'Roles.Name contains an unsupported enum value; migration aborted.', 1;

DECLARE @var nvarchar(max);
SELECT @var = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Roles]') AND [c].[name] = N'Name');
IF @var IS NOT NULL EXEC(N'ALTER TABLE [Roles] DROP CONSTRAINT ' + @var + ';');
ALTER TABLE [Roles] ALTER COLUMN [Name] nvarchar(50) NOT NULL;


UPDATE [Roles] SET [Name] = CASE [Name] WHEN N'0' THEN N'User' WHEN N'1' THEN N'Admin' END;
IF EXISTS (SELECT [Name] FROM [Roles] GROUP BY [Name] HAVING COUNT(*) > 1)
    THROW 51002, 'Role names would not be unique after enum conversion; migration aborted.', 1;

CREATE UNIQUE INDEX [IX_Roles_Name] ON [Roles] ([Name]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261108122631_ConvertRoleNameToString', N'10.0.10');

COMMIT;
GO

