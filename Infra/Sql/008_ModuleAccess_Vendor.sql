-- 008 : "Module Access" menu for Vendor (role 3) too (same rights as Super Admin, changes apply to all vendors).
--       Adds RoleMenuAccess for role 3 (new vendor users copy it) and UserMenuAccess for existing vendor users,
--       and clears the Super Admin only flag set by 007 so the menu stays assignable on the Role / User Access screens.
--       Admin (role 2), Customer and Employee stay without the menu (and are denied by the controller).
-- Re-runnable : skipped when already logged in SchemaScriptLog; inserts are NOT EXISTS guarded.

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
SET NOCOUNT ON;
GO

-- Safety : only LeozApp (or the database named in deploy_all.sql through SESSION_CONTEXT 'Leoz.TargetDb')
DECLARE @Db SYSNAME = DB_NAME(), @Script NVARCHAR(128) = N'008_ModuleAccess_Vendor';
IF @Db <> N'LeozApp' AND @Db <> ISNULL(CAST(SESSION_CONTEXT(N'Leoz.TargetDb') AS SYSNAME), N'')
BEGIN
	RAISERROR(N'%s : database "%s" is not LeozApp. Aborted.', 16, 1, @Script, @Db);
	SET NOEXEC ON;
END
ELSE IF OBJECT_ID(N'[dbo].[SchemaScriptLog]', N'U') IS NULL
	OR NOT EXISTS (SELECT 1 FROM [dbo].[SchemaScriptLog] WHERE [ScriptName] = N'007_ModuleAccess_SuperAdminOnly')
BEGIN
	RAISERROR(N'%s : run 000 to 007 first. Aborted.', 16, 1, @Script);
	SET NOEXEC ON;
END
GO

IF EXISTS (SELECT 1 FROM [dbo].[SchemaScriptLog] WHERE [ScriptName] = N'008_ModuleAccess_Vendor')
BEGIN
	PRINT '008_ModuleAccess_Vendor : already applied, skipped.';
	SET NOEXEC ON;
END
GO

BEGIN TRANSACTION;

DECLARE @MenuId BIGINT = (SELECT TOP 1 [Id] FROM [dbo].[Menu] WHERE [Area] = N'Admin' AND [Controller] = N'ModuleAccess' AND [IsDeleted] = 0 ORDER BY [Id]);

IF @MenuId IS NULL
	THROW 50080, '008_ModuleAccess_Vendor : menu "Module Access" not found (run 004).', 1;

UPDATE [dbo].[Menu] SET [IsSuperAdmin] = 0, [LastModifiedBy] = 1, [LastModifiedDate] = GETDATE() WHERE [Id] = @MenuId;

INSERT INTO [dbo].[RoleMenuAccess] ([RoleId], [MenuId], [IsRead], [IsCreate], [IsUpdate], [IsDelete], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [IsActive], [IsDeleted])
SELECT 3, @MenuId, 1, 1, 1, 0, 1, GETDATE(), 1, GETDATE(), 1, 0
WHERE EXISTS (SELECT 1 FROM [dbo].[Roles] WHERE [Id] = 3)
AND NOT EXISTS (SELECT 1 FROM [dbo].[RoleMenuAccess] x WHERE x.[RoleId] = 3 AND x.[MenuId] = @MenuId);

PRINT CONCAT('RoleMenuAccess : ', @@ROWCOUNT, ' row(s) inserted.');

INSERT INTO [dbo].[UserMenuAccess] ([UserId], [RoleId], [MenuId], [IsRead], [IsCreate], [IsUpdate], [IsDelete], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [IsActive], [IsDeleted])
SELECT DISTINCT m.[UserId], m.[RoleId], @MenuId, 1, 1, 1, 0, 1, GETDATE(), 1, GETDATE(), 1, 0
FROM [dbo].[UserRoleMapping] m
WHERE m.[RoleId] = 3 AND m.[IsActive] = 1 AND m.[IsDeleted] = 0
AND NOT EXISTS (SELECT 1 FROM [dbo].[UserMenuAccess] x WHERE x.[UserId] = m.[UserId] AND x.[RoleId] = m.[RoleId] AND x.[MenuId] = @MenuId);

PRINT CONCAT('UserMenuAccess : ', @@ROWCOUNT, ' row(s) inserted.');

INSERT INTO [dbo].[SchemaScriptLog] ([ScriptName], [AppliedOn]) VALUES (N'008_ModuleAccess_Vendor', SYSDATETIME());

COMMIT TRANSACTION;
PRINT '008_ModuleAccess_Vendor : applied.';
GO

SET NOEXEC OFF;
GO
