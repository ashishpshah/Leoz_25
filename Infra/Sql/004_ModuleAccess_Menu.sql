-- 004 : "Module Access" menu (Areas/Admin/Controllers/ModuleAccessController) under Configuration,
--       readable by Admin (role 2) and Vendor (role 3). Super Admin (role 1) sees every menu.
--       Existing Admin / Vendor users get the menu in UserMenuAccess (new users copy RoleMenuAccess).
-- Re-runnable : skipped when already logged in SchemaScriptLog; inserts are NOT EXISTS guarded.

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
SET NOCOUNT ON;
GO

-- Safety : only LeozApp (or the database named in deploy_all.sql through SESSION_CONTEXT 'Leoz.TargetDb')
DECLARE @Db SYSNAME = DB_NAME(), @Script NVARCHAR(128) = N'004_ModuleAccess_Menu';
IF @Db <> N'LeozApp' AND @Db <> ISNULL(CAST(SESSION_CONTEXT(N'Leoz.TargetDb') AS SYSNAME), N'')
BEGIN
	RAISERROR(N'%s : database "%s" is not LeozApp. Aborted.', 16, 1, @Script, @Db);
	SET NOEXEC ON;
END
ELSE IF OBJECT_ID(N'[dbo].[SchemaScriptLog]', N'U') IS NULL
	OR NOT EXISTS (SELECT 1 FROM [dbo].[SchemaScriptLog] WHERE [ScriptName] = N'003_LOV_DesignationCode_Backfill')
BEGIN
	RAISERROR(N'%s : run 000 to 003 first. Aborted.', 16, 1, @Script);
	SET NOEXEC ON;
END
GO

IF EXISTS (SELECT 1 FROM [dbo].[SchemaScriptLog] WHERE [ScriptName] = N'004_ModuleAccess_Menu')
BEGIN
	PRINT '004_ModuleAccess_Menu : already applied, skipped.';
	SET NOEXEC ON;
END
GO

BEGIN TRANSACTION;

DECLARE @ParentId BIGINT = (SELECT TOP 1 [Id] FROM [dbo].[Menu] WHERE [ParentId] = 0 AND [Name] = N'Configuration' AND [IsDeleted] = 0 ORDER BY [Id]);

IF @ParentId IS NULL
	THROW 50040, '004_ModuleAccess_Menu : parent menu "Configuration" not found.', 1;

IF NOT EXISTS (SELECT 1 FROM [dbo].[Menu] WHERE [Area] = N'Admin' AND [Controller] = N'ModuleAccess' AND [IsDeleted] = 0)
	INSERT INTO [dbo].[Menu] ([ParentId], [Area], [Controller], [Url], [Name], [Icon], [DisplayOrder], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [IsSuperAdmin], [IsAdmin], [IsActive], [IsDeleted])
	VALUES (@ParentId, N'Admin', N'ModuleAccess', NULL, N'Module Access', N'bi bi-ui-checks-grid', 6, 1, GETDATE(), 1, GETDATE(), 0, 0, 1, 0);

DECLARE @MenuId BIGINT = (SELECT TOP 1 [Id] FROM [dbo].[Menu] WHERE [Area] = N'Admin' AND [Controller] = N'ModuleAccess' AND [IsDeleted] = 0 ORDER BY [Id]);

-- Role access : Admin (2), Vendor (3). Delete flag is not used by this screen.
INSERT INTO [dbo].[RoleMenuAccess] ([RoleId], [MenuId], [IsRead], [IsCreate], [IsUpdate], [IsDelete], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [IsActive], [IsDeleted])
SELECT r.[Id], @MenuId, 1, 1, 1, 0, 1, GETDATE(), 1, GETDATE(), 1, 0
FROM [dbo].[Roles] r
WHERE r.[Id] IN (2, 3)
AND NOT EXISTS (SELECT 1 FROM [dbo].[RoleMenuAccess] x WHERE x.[RoleId] = r.[Id] AND x.[MenuId] = @MenuId);

PRINT CONCAT('RoleMenuAccess : ', @@ROWCOUNT, ' row(s) inserted.');

-- Existing Admin / Vendor users (menus are loaded from UserMenuAccess at login)
INSERT INTO [dbo].[UserMenuAccess] ([UserId], [RoleId], [MenuId], [IsRead], [IsCreate], [IsUpdate], [IsDelete], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [IsActive], [IsDeleted])
SELECT DISTINCT m.[UserId], m.[RoleId], @MenuId, 1, 1, 1, 0, 1, GETDATE(), 1, GETDATE(), 1, 0
FROM [dbo].[UserRoleMapping] m
WHERE m.[RoleId] IN (2, 3) AND m.[IsActive] = 1 AND m.[IsDeleted] = 0
AND NOT EXISTS (SELECT 1 FROM [dbo].[UserMenuAccess] x WHERE x.[UserId] = m.[UserId] AND x.[RoleId] = m.[RoleId] AND x.[MenuId] = @MenuId);

PRINT CONCAT('UserMenuAccess : ', @@ROWCOUNT, ' row(s) inserted.');

INSERT INTO [dbo].[SchemaScriptLog] ([ScriptName], [AppliedOn]) VALUES (N'004_ModuleAccess_Menu', SYSDATETIME());

COMMIT TRANSACTION;
PRINT '004_ModuleAccess_Menu : applied.';
GO

SET NOEXEC OFF;
GO
