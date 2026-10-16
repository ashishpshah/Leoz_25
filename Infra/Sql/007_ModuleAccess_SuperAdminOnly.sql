-- 007 : "Module Access" menu for Super Admin only. Removes the menu (added by 004) from every other role and user
--       (Admin, Vendor, ...). Super Admin (role 1) sees every menu without RoleMenuAccess / UserMenuAccess rows.
--       Only rows of the Module Access menu are touched.
-- Re-runnable : skipped when already logged in SchemaScriptLog.

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
SET NOCOUNT ON;
GO

-- Safety : only LeozApp (or the database named in deploy_all.sql through SESSION_CONTEXT 'Leoz.TargetDb')
DECLARE @Db SYSNAME = DB_NAME(), @Script NVARCHAR(128) = N'007_ModuleAccess_SuperAdminOnly';
IF @Db <> N'LeozApp' AND @Db <> ISNULL(CAST(SESSION_CONTEXT(N'Leoz.TargetDb') AS SYSNAME), N'')
BEGIN
	RAISERROR(N'%s : database "%s" is not LeozApp. Aborted.', 16, 1, @Script, @Db);
	SET NOEXEC ON;
END
ELSE IF OBJECT_ID(N'[dbo].[SchemaScriptLog]', N'U') IS NULL
	OR NOT EXISTS (SELECT 1 FROM [dbo].[SchemaScriptLog] WHERE [ScriptName] = N'004_ModuleAccess_Menu')
BEGIN
	RAISERROR(N'%s : run 000 to 004 first. Aborted.', 16, 1, @Script);
	SET NOEXEC ON;
END
GO

IF EXISTS (SELECT 1 FROM [dbo].[SchemaScriptLog] WHERE [ScriptName] = N'007_ModuleAccess_SuperAdminOnly')
BEGIN
	PRINT '007_ModuleAccess_SuperAdminOnly : already applied, skipped.';
	SET NOEXEC ON;
END
GO

BEGIN TRANSACTION;

DECLARE @MenuId BIGINT = (SELECT TOP 1 [Id] FROM [dbo].[Menu] WHERE [Area] = N'Admin' AND [Controller] = N'ModuleAccess' ORDER BY [Id]);

IF @MenuId IS NOT NULL
BEGIN
	DELETE FROM [dbo].[UserMenuAccess] WHERE [MenuId] = @MenuId AND [RoleId] <> 1;
	PRINT CONCAT('UserMenuAccess : ', @@ROWCOUNT, ' row(s) removed.');

	DELETE FROM [dbo].[RoleMenuAccess] WHERE [MenuId] = @MenuId AND [RoleId] <> 1;
	PRINT CONCAT('RoleMenuAccess : ', @@ROWCOUNT, ' row(s) removed.');

	-- Super Admin only menu flag
	UPDATE [dbo].[Menu] SET [IsSuperAdmin] = 1, [LastModifiedBy] = 1, [LastModifiedDate] = GETDATE() WHERE [Id] = @MenuId;
END

INSERT INTO [dbo].[SchemaScriptLog] ([ScriptName], [AppliedOn]) VALUES (N'007_ModuleAccess_SuperAdminOnly', SYSDATETIME());

COMMIT TRANSACTION;
PRINT '007_ModuleAccess_SuperAdminOnly : applied.';
GO

SET NOEXEC OFF;
GO
