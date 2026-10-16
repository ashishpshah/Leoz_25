-- 005 : display name of module SITE = "Daily Update (Site Updates)" so it matches the Daily Update menu on the Module Access screen.
--       Rights are unchanged (system defaults seeded by 002 / 003).
-- Re-runnable : skipped when already logged in SchemaScriptLog.

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
SET NOCOUNT ON;
GO

-- Safety : only LeozApp (or the database named in deploy_all.sql through SESSION_CONTEXT 'Leoz.TargetDb')
DECLARE @Db SYSNAME = DB_NAME(), @Script NVARCHAR(128) = N'005_ProjectModule_DailyUpdate_Name';
IF @Db <> N'LeozApp' AND @Db <> ISNULL(CAST(SESSION_CONTEXT(N'Leoz.TargetDb') AS SYSNAME), N'')
BEGIN
	RAISERROR(N'%s : database "%s" is not LeozApp. Aborted.', 16, 1, @Script, @Db);
	SET NOEXEC ON;
END
ELSE IF OBJECT_ID(N'[dbo].[SchemaScriptLog]', N'U') IS NULL
	OR NOT EXISTS (SELECT 1 FROM [dbo].[SchemaScriptLog] WHERE [ScriptName] = N'002_ProjectPermissions_Seed')
BEGIN
	RAISERROR(N'%s : run 000 to 002 first. Aborted.', 16, 1, @Script);
	SET NOEXEC ON;
END
GO

IF EXISTS (SELECT 1 FROM [dbo].[SchemaScriptLog] WHERE [ScriptName] = N'005_ProjectModule_DailyUpdate_Name')
BEGIN
	PRINT '005_ProjectModule_DailyUpdate_Name : already applied, skipped.';
	SET NOEXEC ON;
END
GO

BEGIN TRANSACTION;

UPDATE [dbo].[ProjectModule] SET [Name] = N'Daily Update (Site Updates)' WHERE [Code] = 'SITE' AND [Name] = N'Site Updates';

INSERT INTO [dbo].[SchemaScriptLog] ([ScriptName], [AppliedOn]) VALUES (N'005_ProjectModule_DailyUpdate_Name', SYSDATETIME());

COMMIT TRANSACTION;
PRINT '005_ProjectModule_DailyUpdate_Name : applied.';
GO

SET NOEXEC OFF;
GO
