-- 006 : separate the Daily Update screen from the Project Detail box "Site Updates".
--   SITE  = Project Detail box "Site Updates" : a DOCUMENT module (ProjectSiteDoc Type SITE, Save_Doc / Save_Doc_Status "DOC"),
--           so it gets APPROVE / REJECT and the legacy document rights (CanUpload / CanModify / CanApproveDoc).
--   DAILY = Daily Update screen (menu "Daily Update", ProjectController.DailyUpdate / Save_DailyUpdate) : VIEW, ADD, EDIT.
--           VIEW : every Vendor / Customer / Employee (legacy : menu access only, no server check). ADD / EDIT : Co-ordinator.
--   Also restores the SITE display name changed by 005. SITE VIEW rows (from LOV) are unchanged.
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
DECLARE @Db SYSNAME = DB_NAME(), @Script NVARCHAR(128) = N'006_DailyUpdate_Module_Split';
IF @Db NOT IN (N'LeozApp', N'LeozApp_Test') AND @Db <> ISNULL(CAST(SESSION_CONTEXT(N'Leoz.TargetDb') AS SYSNAME), N'')
BEGIN
	RAISERROR(N'%s : database "%s" is not LeozApp / LeozApp_Test. Aborted.', 16, 1, @Script, @Db);
	SET NOEXEC ON;
END
ELSE IF OBJECT_ID(N'[dbo].[SchemaScriptLog]', N'U') IS NULL
	OR NOT EXISTS (SELECT 1 FROM [dbo].[SchemaScriptLog] WHERE [ScriptName] = N'003_LOV_DesignationCode_Backfill')
BEGIN
	RAISERROR(N'%s : run 000 to 003 first. Aborted.', 16, 1, @Script);
	SET NOEXEC ON;
END
GO

IF EXISTS (SELECT 1 FROM [dbo].[SchemaScriptLog] WHERE [ScriptName] = N'006_DailyUpdate_Module_Split')
BEGIN
	PRINT '006_DailyUpdate_Module_Split : already applied, skipped.';
	SET NOEXEC ON;
END
GO

BEGIN TRANSACTION;

-- -------------------------------------------------------------------------------------
-- Modules and actions
-- -------------------------------------------------------------------------------------
UPDATE [dbo].[ProjectModule] SET [Name] = N'Site Updates' WHERE [Code] = 'SITE' AND [Name] = N'Daily Update (Site Updates)';

IF NOT EXISTS (SELECT 1 FROM [dbo].[ProjectModule] WHERE [Code] = 'DAILY')
	INSERT INTO [dbo].[ProjectModule] ([Code], [Name], [DisplayOrder], [IsActive]) VALUES ('DAILY', N'Daily Update', 10, 1);

INSERT INTO [dbo].[ProjectModuleAction] ([ModuleCode], [ActionCode])
SELECT s.[ModuleCode], s.[ActionCode]
FROM (VALUES ('SITE', 'APPROVE'), ('SITE', 'REJECT'), ('DAILY', 'VIEW'), ('DAILY', 'ADD'), ('DAILY', 'EDIT')) s ([ModuleCode], [ActionCode])
WHERE NOT EXISTS (SELECT 1 FROM [dbo].[ProjectModuleAction] x WHERE x.[ModuleCode] = s.[ModuleCode] AND x.[ActionCode] = s.[ActionCode]);

-- -------------------------------------------------------------------------------------
-- Move the Daily Update rights (seeded on SITE by 002) to DAILY : SITE EDIT for Co-ordinator is not a legacy document right
-- -------------------------------------------------------------------------------------
DELETE FROM [dbo].[ProjectModulePermission]
WHERE [VendorId] IS NULL AND [SubjectType] = 'DESIGNATION' AND [SubjectCode] = 'COORDINATOR' AND [ModuleCode] = 'SITE' AND [ActionCode] = 'EDIT';

PRINT CONCAT('ProjectModulePermission : ', @@ROWCOUNT, ' SITE EDIT (Co-ordinator) system-default row(s) removed.');

-- -------------------------------------------------------------------------------------
-- System-default rights (VendorId NULL), allowed rows only
-- -------------------------------------------------------------------------------------
DECLARE @Subject TABLE ([SubjectCode] VARCHAR(30) PRIMARY KEY, [SubjectType] VARCHAR(12) NOT NULL);

INSERT INTO @Subject ([SubjectCode], [SubjectType]) VALUES
	('VENDOR', 'ACTOR'), ('CUSTOMER', 'ACTOR'),
	('MANAGER', 'DESIGNATION'), ('COORDINATOR', 'DESIGNATION'), ('SITE_SUPERVISOR', 'DESIGNATION'), ('SITE_ENGINEER', 'DESIGNATION'),
	('PURCHASER', 'DESIGNATION'), ('ARCHITECT', 'DESIGNATION'), ('SITE_VENDOR', 'DESIGNATION'), ('OTHER', 'DESIGNATION');

DECLARE @Rule TABLE ([Modules] VARCHAR(200), [Actions] VARCHAR(200), [Subjects] VARCHAR(300));

INSERT INTO @Rule ([Modules], [Actions], [Subjects]) VALUES
	-- Site Updates box : same document rules as EPM / MB (CanUpload, CanUpload + CanModify, CanApproveDoc)
	('SITE', 'ADD',            'VENDOR,CUSTOMER,MANAGER,COORDINATOR,SITE_ENGINEER,ARCHITECT'),
	('SITE', 'EDIT',           'VENDOR,CUSTOMER,MANAGER'),
	('SITE', 'APPROVE,REJECT', 'VENDOR,CUSTOMER,MANAGER,COORDINATOR'),

	-- Daily Update screen
	('DAILY', 'VIEW',     'VENDOR,CUSTOMER,MANAGER,COORDINATOR,SITE_SUPERVISOR,SITE_ENGINEER,PURCHASER,ARCHITECT,SITE_VENDOR,OTHER'),
	('DAILY', 'ADD,EDIT', 'COORDINATOR');

DECLARE @Seed TABLE ([SubjectType] VARCHAR(12), [SubjectCode] VARCHAR(30), [ModuleCode] VARCHAR(10), [ActionCode] VARCHAR(20));

INSERT INTO @Seed ([SubjectType], [SubjectCode], [ModuleCode], [ActionCode])
SELECT sub.[SubjectType], sub.[SubjectCode], TRIM(m.[value]), TRIM(a.[value])
FROM @Rule r
CROSS APPLY STRING_SPLIT(r.[Modules], ',') m
CROSS APPLY STRING_SPLIT(r.[Actions], ',') a
CROSS APPLY STRING_SPLIT(r.[Subjects], ',') s
JOIN @Subject sub ON sub.[SubjectCode] = TRIM(s.[value]);

IF (SELECT COUNT(*) FROM @Rule r CROSS APPLY STRING_SPLIT(r.[Modules], ',') m CROSS APPLY STRING_SPLIT(r.[Actions], ',') a CROSS APPLY STRING_SPLIT(r.[Subjects], ',') s)
	<> (SELECT COUNT(*) FROM @Seed)
	THROW 50061, 'Seed contains an unknown subject code.', 1;

IF EXISTS (SELECT 1 FROM @Seed s WHERE NOT EXISTS (SELECT 1 FROM [dbo].[ProjectModuleAction] x WHERE x.[ModuleCode] = s.[ModuleCode] AND x.[ActionCode] = s.[ActionCode]))
	THROW 50062, 'Seed contains a module/action that is not defined in ProjectModuleAction.', 1;

INSERT INTO [dbo].[ProjectModulePermission] ([VendorId], [SubjectType], [SubjectCode], [ModuleCode], [ActionCode], [IsAllowed], [Scope], [IsActive], [IsDeleted], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate])
SELECT NULL, s.[SubjectType], s.[SubjectCode], s.[ModuleCode], s.[ActionCode], 1, 'ALL', 1, 0, 1, GETDATE(), 1, GETDATE()
FROM @Seed s
WHERE NOT EXISTS (SELECT 1 FROM [dbo].[ProjectModulePermission] p
				  WHERE p.[VendorId] IS NULL AND p.[SubjectType] = s.[SubjectType] AND p.[SubjectCode] = s.[SubjectCode]
				  AND p.[ModuleCode] = s.[ModuleCode] AND p.[ActionCode] = s.[ActionCode]);

PRINT CONCAT('ProjectModulePermission : ', @@ROWCOUNT, ' system-default row(s) inserted.');

UPDATE [dbo].[PermissionVersion] SET [Version] = [Version] + 1 WHERE [Id] = 1;

INSERT INTO [dbo].[SchemaScriptLog] ([ScriptName], [AppliedOn]) VALUES (N'006_DailyUpdate_Module_Split', SYSDATETIME());

COMMIT TRANSACTION;
PRINT '006_DailyUpdate_Module_Split : applied.';
GO

SET NOEXEC OFF;
GO
