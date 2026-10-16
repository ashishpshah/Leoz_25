-- 007 : action HISTORY = "Recent Uploads" table (previous versions) of the history document modules F3D, DRW, MB.
--       Default = same as VIEW (legacy : everyone who can open the module sees the table), copied from the VIEW rows
--       (system defaults and vendor overrides), so nothing changes until HISTORY is unticked on the Module Access screen.
-- Re-runnable : skipped when already logged in SchemaScriptLog; inserts are NOT EXISTS guarded.

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
SET XACT_ABORT ON;
SET NOCOUNT ON;
GO

-- Safety : only LeozApp / LeozApp_Test (or the database named in deploy_all.sql through SESSION_CONTEXT 'Leoz.TargetDb')
DECLARE @Db SYSNAME = DB_NAME(), @Script NVARCHAR(128) = N'007_History_Action';
IF @Db NOT IN (N'LeozApp', N'LeozApp_Test') AND @Db <> ISNULL(CAST(SESSION_CONTEXT(N'Leoz.TargetDb') AS SYSNAME), N'')
BEGIN
	RAISERROR(N'%s : database "%s" is not LeozApp / LeozApp_Test. Aborted.', 16, 1, @Script, @Db);
	SET NOEXEC ON;
END
ELSE IF OBJECT_ID(N'[dbo].[SchemaScriptLog]', N'U') IS NULL
	OR NOT EXISTS (SELECT 1 FROM [dbo].[SchemaScriptLog] WHERE [ScriptName] = N'006_DailyUpdate_Module_Split')
BEGIN
	RAISERROR(N'%s : run 000 to 006 first. Aborted.', 16, 1, @Script);
	SET NOEXEC ON;
END
GO

IF EXISTS (SELECT 1 FROM [dbo].[SchemaScriptLog] WHERE [ScriptName] = N'007_History_Action')
BEGIN
	PRINT '007_History_Action : already applied, skipped.';
	SET NOEXEC ON;
END
GO

BEGIN TRANSACTION;

IF NOT EXISTS (SELECT 1 FROM [dbo].[PermissionAction] WHERE [Code] = 'HISTORY')
	INSERT INTO [dbo].[PermissionAction] ([Code], [Name]) VALUES ('HISTORY', N'Recent Uploads');

INSERT INTO [dbo].[ProjectModuleAction] ([ModuleCode], [ActionCode])
SELECT s.[ModuleCode], 'HISTORY'
FROM (VALUES ('F3D'), ('DRW'), ('MB')) s ([ModuleCode])
WHERE NOT EXISTS (SELECT 1 FROM [dbo].[ProjectModuleAction] x WHERE x.[ModuleCode] = s.[ModuleCode] AND x.[ActionCode] = 'HISTORY');

-- HISTORY = VIEW, for system defaults and vendor overrides
INSERT INTO [dbo].[ProjectModulePermission] ([VendorId], [SubjectType], [SubjectCode], [ModuleCode], [ActionCode], [IsAllowed], [Scope], [IsActive], [IsDeleted], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate])
SELECT v.[VendorId], v.[SubjectType], v.[SubjectCode], v.[ModuleCode], 'HISTORY', v.[IsAllowed], 'ALL', v.[IsActive], v.[IsDeleted], 1, GETDATE(), 1, GETDATE()
FROM [dbo].[ProjectModulePermission] v
WHERE v.[ModuleCode] IN ('F3D', 'DRW', 'MB') AND v.[ActionCode] = 'VIEW'
AND NOT EXISTS (SELECT 1 FROM [dbo].[ProjectModulePermission] p
				WHERE ISNULL(p.[VendorId], 0) = ISNULL(v.[VendorId], 0) AND p.[SubjectType] = v.[SubjectType] AND p.[SubjectCode] = v.[SubjectCode]
				AND p.[ModuleCode] = v.[ModuleCode] AND p.[ActionCode] = 'HISTORY');

PRINT CONCAT('ProjectModulePermission : ', @@ROWCOUNT, ' HISTORY row(s) inserted (copied from VIEW).');

UPDATE [dbo].[PermissionVersion] SET [Version] = [Version] + 1 WHERE [Id] = 1;

INSERT INTO [dbo].[SchemaScriptLog] ([ScriptName], [AppliedOn]) VALUES (N'007_History_Action', SYSDATETIME());

COMMIT TRANSACTION;
PRINT '007_History_Action : applied.';
GO

SET NOEXEC OFF;
GO
