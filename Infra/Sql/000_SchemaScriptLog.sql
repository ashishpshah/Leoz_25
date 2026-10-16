-- 000 : SchemaScriptLog - records which numbered scripts of Infra/Sql have been applied (see docs/permissions.md).
-- Re-runnable. Apply with Infra/Sql/run-local.ps1 (local) or Infra/Sql/deploy_all.sql (production).

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
SET NOCOUNT ON;
GO

-- Safety : only LeozApp (or the database named in deploy_all.sql through SESSION_CONTEXT 'Leoz.TargetDb')
DECLARE @Db SYSNAME = DB_NAME(), @Script NVARCHAR(128) = N'000_SchemaScriptLog';
IF @Db <> N'LeozApp' AND @Db <> ISNULL(CAST(SESSION_CONTEXT(N'Leoz.TargetDb') AS SYSNAME), N'')
BEGIN
	RAISERROR(N'%s : database "%s" is not LeozApp. Aborted.', 16, 1, @Script, @Db);
	SET NOEXEC ON;
END
GO

IF OBJECT_ID(N'[dbo].[SchemaScriptLog]', N'U') IS NULL
BEGIN
	CREATE TABLE [dbo].[SchemaScriptLog] (
		[ScriptName] NVARCHAR(128) NOT NULL,
		[AppliedOn] DATETIME2 NOT NULL CONSTRAINT [DF_SchemaScriptLog_AppliedOn] DEFAULT (SYSDATETIME()),
		CONSTRAINT [PK_SchemaScriptLog] PRIMARY KEY ([ScriptName])
	);
END
GO

IF NOT EXISTS (SELECT 1 FROM [dbo].[SchemaScriptLog] WHERE [ScriptName] = N'000_SchemaScriptLog')
BEGIN
	INSERT INTO [dbo].[SchemaScriptLog] ([ScriptName], [AppliedOn]) VALUES (N'000_SchemaScriptLog', SYSDATETIME());
	PRINT '000_SchemaScriptLog : applied.';
END
ELSE
	PRINT '000_SchemaScriptLog : already applied.';
GO

SET NOEXEC OFF;
GO
