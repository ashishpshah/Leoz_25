-- 009 : table OfflineOperation for the PWA offline sync (OfflineOperationFilter).
--       Each change saved offline is sent with a unique operation id (header X-Leoz-Operation-Id).
--       The first request runs the action and keeps its answer here; a repeated request (e.g. the answer was lost)
--       gets the same answer back instead of saving the record a second time (no duplicate Projects).
--       Only successful saves are kept; rows older than 30 days are removed by the application.
-- Re-runnable : skipped when already logged in SchemaScriptLog.

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
SET NOCOUNT ON;
GO

-- Safety : only LeozApp (or the database named in deploy_all.sql through SESSION_CONTEXT 'Leoz.TargetDb')
DECLARE @Db SYSNAME = DB_NAME(), @Script NVARCHAR(128) = N'009_OfflineOperation';
IF @Db <> N'LeozApp' AND @Db <> ISNULL(CAST(SESSION_CONTEXT(N'Leoz.TargetDb') AS SYSNAME), N'')
BEGIN
	RAISERROR(N'%s : database "%s" is not LeozApp. Aborted.', 16, 1, @Script, @Db);
	SET NOEXEC ON;
END
ELSE IF OBJECT_ID(N'[dbo].[SchemaScriptLog]', N'U') IS NULL
	OR NOT EXISTS (SELECT 1 FROM [dbo].[SchemaScriptLog] WHERE [ScriptName] = N'008_ModuleAccess_Vendor')
BEGIN
	RAISERROR(N'%s : run 000 to 008 first. Aborted.', 16, 1, @Script);
	SET NOEXEC ON;
END
GO

IF EXISTS (SELECT 1 FROM [dbo].[SchemaScriptLog] WHERE [ScriptName] = N'009_OfflineOperation')
BEGIN
	PRINT '009_OfflineOperation : already applied, skipped.';
	SET NOEXEC ON;
END
GO

BEGIN TRANSACTION;

IF OBJECT_ID(N'[dbo].[OfflineOperation]', N'U') IS NULL
BEGIN
	CREATE TABLE [dbo].[OfflineOperation]
	(
		[OperationId]  UNIQUEIDENTIFIER NOT NULL,
		[UserId]       BIGINT           NOT NULL,
		[Path]         NVARCHAR(256)    NOT NULL,
		[Status]       TINYINT          NOT NULL,   -- 0 = running, 1 = completed
		[ResponseJson] NVARCHAR(MAX)    NULL,
		[CreatedOn]    DATETIME2(3)     NOT NULL,
		[CompletedOn]  DATETIME2(3)     NULL,
		CONSTRAINT [PK_OfflineOperation] PRIMARY KEY CLUSTERED ([OperationId])
	);

	CREATE NONCLUSTERED INDEX [IX_OfflineOperation_CreatedOn] ON [dbo].[OfflineOperation] ([CreatedOn]);
END

INSERT INTO [dbo].[SchemaScriptLog] ([ScriptName], [AppliedOn]) VALUES (N'009_OfflineOperation', SYSDATETIME());

COMMIT TRANSACTION;
PRINT '009_OfflineOperation : applied.';
GO

SET NOEXEC OFF;
GO
