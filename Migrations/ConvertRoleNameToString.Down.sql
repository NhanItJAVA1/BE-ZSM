BEGIN TRANSACTION;

IF EXISTS (SELECT 1 FROM [Roles] WHERE [Name] NOT IN (N'User', N'Admin'))
    THROW 51003, 'Roles.Name contains a value that cannot be converted back to the enum.', 1;
IF EXISTS (SELECT [Name] FROM [Roles] GROUP BY [Name] HAVING COUNT(*) > 1)
    THROW 51004, 'Duplicate role names prevent rollback.', 1;

DROP INDEX [IX_Roles_Name] ON [Roles];

UPDATE [Roles] SET [Name] = CASE [Name] WHEN N'User' THEN N'0' WHEN N'Admin' THEN N'1' END;

DECLARE @var nvarchar(max);
SELECT @var = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Roles]') AND [c].[name] = N'Name');
IF @var IS NOT NULL EXEC(N'ALTER TABLE [Roles] DROP CONSTRAINT ' + @var + ';');
ALTER TABLE [Roles] ALTER COLUMN [Name] int NOT NULL;

DELETE FROM [__EFMigrationsHistory]
WHERE [MigrationId] = N'20261108122631_ConvertRoleNameToString';

COMMIT;
GO

