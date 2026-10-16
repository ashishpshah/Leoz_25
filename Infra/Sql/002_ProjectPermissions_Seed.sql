-- 002 : Project Detail module permissions - reference data + system-default rights (VendorId NULL) reproducing
--       the legacy rules of Infra/ProjectAccess.cs and ProjectController (see docs/permissions.md).
-- Re-runnable : skipped when already logged in SchemaScriptLog; inserts are NOT EXISTS guarded and never overwrite
-- existing rows (admin changes are kept). Only ALLOWED rows are seeded; anything not listed is denied.
-- Employee VIEW rows come from LOV.ProjectDetailTypeAccess (003_LOV_DesignationCode_Backfill.sql).
-- No DELETE / DELETE_FILE action : delete endpoints keep their legacy checks.

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
DECLARE @Db SYSNAME = DB_NAME(), @Script NVARCHAR(128) = N'002_ProjectPermissions_Seed';
IF @Db NOT IN (N'LeozApp', N'LeozApp_Test') AND @Db <> ISNULL(CAST(SESSION_CONTEXT(N'Leoz.TargetDb') AS SYSNAME), N'')
BEGIN
	RAISERROR(N'%s : database "%s" is not LeozApp / LeozApp_Test. Aborted.', 16, 1, @Script, @Db);
	SET NOEXEC ON;
END
ELSE IF OBJECT_ID(N'[dbo].[SchemaScriptLog]', N'U') IS NULL OR OBJECT_ID(N'[dbo].[ProjectModulePermission]', N'U') IS NULL
BEGIN
	RAISERROR(N'%s : run 000_SchemaScriptLog.sql and 001_ProjectPermissions_Schema.sql first. Aborted.', 16, 1, @Script);
	SET NOEXEC ON;
END
GO

IF EXISTS (SELECT 1 FROM [dbo].[SchemaScriptLog] WHERE [ScriptName] = N'002_ProjectPermissions_Seed')
BEGIN
	PRINT '002_ProjectPermissions_Seed : already applied, skipped.';
	SET NOEXEC ON;
END
GO

BEGIN TRANSACTION;

-- -------------------------------------------------------------------------------------
-- Reference data
-- -------------------------------------------------------------------------------------
INSERT INTO [dbo].[ProjectModule] ([Code], [Name], [DisplayOrder], [IsActive])
SELECT s.[Code], s.[Name], s.[DisplayOrder], 1
FROM (VALUES
	('EPM',    N'Estimate & Payment Terms', 1),
	('F3D',    N'Final 3D', 2),
	('DRW',    N'Drawings', 3),
	('WCLTSC', N'Client Scope of Work & Material Specifications', 4),
	('MB',     N'Mood Board', 5),
	('SITE',   N'Site Updates', 6),
	('WPEND',  N'Pending Selection & Work', 7),
	('MPRO',   N'Material Procurement', 8),
	('WCOMP',  N'Work Completion Report', 9)
) s ([Code], [Name], [DisplayOrder])
WHERE NOT EXISTS (SELECT 1 FROM [dbo].[ProjectModule] m WHERE m.[Code] = s.[Code]);

INSERT INTO [dbo].[PermissionAction] ([Code], [Name])
SELECT s.[Code], s.[Name]
FROM (VALUES
	('VIEW',     N'View'),
	('ADD',      N'Add'),
	('EDIT',     N'Edit'),
	('APPROVE',  N'Approve'),
	('REJECT',   N'Reject'),
	('COMPLETE', N'Complete'),
	('ORDER',    N'Order'),
	('RECEIVE',  N'Receive')
) s ([Code], [Name])
WHERE NOT EXISTS (SELECT 1 FROM [dbo].[PermissionAction] a WHERE a.[Code] = s.[Code]);

INSERT INTO [dbo].[ProjectModuleAction] ([ModuleCode], [ActionCode])
SELECT DISTINCT TRIM(m.[value]), TRIM(a.[value])
FROM (VALUES
	('EPM,F3D,DRW,WCLTSC,MB', 'VIEW,ADD,EDIT,APPROVE,REJECT'),
	('SITE',                  'VIEW,ADD,EDIT'),
	('WPEND',                 'VIEW,ADD,EDIT,COMPLETE,REJECT'),
	('MPRO',                  'VIEW,ADD,EDIT,ORDER,REJECT,RECEIVE'),
	('WCOMP',                 'VIEW,ADD,EDIT,APPROVE,REJECT')
) s ([Modules], [Actions])
CROSS APPLY STRING_SPLIT(s.[Modules], ',') m
CROSS APPLY STRING_SPLIT(s.[Actions], ',') a
WHERE NOT EXISTS (SELECT 1 FROM [dbo].[ProjectModuleAction] x WHERE x.[ModuleCode] = TRIM(m.[value]) AND x.[ActionCode] = TRIM(a.[value]));

-- -------------------------------------------------------------------------------------
-- System-default rights
-- -------------------------------------------------------------------------------------
DECLARE @Subject TABLE ([SubjectCode] VARCHAR(30) PRIMARY KEY, [SubjectType] VARCHAR(12) NOT NULL);

INSERT INTO @Subject ([SubjectCode], [SubjectType]) VALUES
	('VENDOR', 'ACTOR'), ('CUSTOMER', 'ACTOR'),
	('MANAGER', 'DESIGNATION'), ('COORDINATOR', 'DESIGNATION'), ('SITE_SUPERVISOR', 'DESIGNATION'), ('SITE_ENGINEER', 'DESIGNATION'),
	('PURCHASER', 'DESIGNATION'), ('ARCHITECT', 'DESIGNATION'), ('SITE_VENDOR', 'DESIGNATION'), ('OTHER', 'DESIGNATION');

DECLARE @Rule TABLE ([Modules] VARCHAR(200), [Actions] VARCHAR(200), [Subjects] VARCHAR(300));

INSERT INTO @Rule ([Modules], [Actions], [Subjects]) VALUES
	-- Visibility : Vendor / Customer see all modules (employees : 003)
	('EPM,F3D,DRW,WCLTSC,MB,SITE,WPEND,MPRO,WCOMP', 'VIEW', 'VENDOR,CUSTOMER'),

	-- Document modules (legacy CanUpload / CanModify / CanApproveDoc / CanAddClientScope in Save_Doc, Save_Doc_Status "DOC")
	('EPM,F3D,DRW,MB',        'ADD',            'VENDOR,CUSTOMER,MANAGER,COORDINATOR,SITE_ENGINEER,ARCHITECT'),
	('WCLTSC',                'ADD',            'VENDOR'),
	('EPM,WCLTSC',            'EDIT',           'VENDOR,CUSTOMER,MANAGER'),
	('F3D,DRW,MB',            'EDIT',           'VENDOR,CUSTOMER,MANAGER,COORDINATOR,SITE_ENGINEER,ARCHITECT'),
	('EPM,F3D,DRW,WCLTSC,MB', 'APPROVE,REJECT', 'VENDOR,CUSTOMER,MANAGER,COORDINATOR'),

	-- Site Updates (Save_DailyUpdate : Employee.UserType = "COORD")
	('SITE', 'ADD,EDIT', 'COORDINATOR'),

	-- Pending Selection & Work (Save_WP : never Customer; Save_Doc_Status "WP" : CanModify)
	('WPEND', 'ADD',             'VENDOR,MANAGER,COORDINATOR,SITE_ENGINEER,ARCHITECT'),
	('WPEND', 'EDIT',            'VENDOR,MANAGER'),
	('WPEND', 'COMPLETE,REJECT', 'VENDOR,CUSTOMER,MANAGER'),

	-- Material Procurement (CanAddMaterial / CanUpdateMaterial / CanReceiveMaterial)
	('MPRO', 'ADD',               'VENDOR,CUSTOMER,MANAGER,COORDINATOR,SITE_SUPERVISOR,SITE_ENGINEER,SITE_VENDOR,OTHER'),
	('MPRO', 'EDIT,ORDER,REJECT', 'MANAGER'),
	('MPRO', 'RECEIVE',           'COORDINATOR'),

	-- Work Completion Report (APPROVE / REJECT : Customer only, intentional tightening of Save_Doc_Status "WC")
	('WCOMP', 'ADD,EDIT',       'MANAGER'),
	('WCOMP', 'APPROVE,REJECT', 'CUSTOMER');

DECLARE @Seed TABLE ([SubjectType] VARCHAR(12), [SubjectCode] VARCHAR(30), [ModuleCode] VARCHAR(10), [ActionCode] VARCHAR(20));

INSERT INTO @Seed ([SubjectType], [SubjectCode], [ModuleCode], [ActionCode])
SELECT sub.[SubjectType], sub.[SubjectCode], TRIM(m.[value]), TRIM(a.[value])
FROM @Rule r
CROSS APPLY STRING_SPLIT(r.[Modules], ',') m
CROSS APPLY STRING_SPLIT(r.[Actions], ',') a
CROSS APPLY STRING_SPLIT(r.[Subjects], ',') s
JOIN @Subject sub ON sub.[SubjectCode] = TRIM(s.[value]);

-- Guards : every subject token known, every (module, action) defined, no duplicates
IF (SELECT COUNT(*) FROM @Rule r CROSS APPLY STRING_SPLIT(r.[Modules], ',') m CROSS APPLY STRING_SPLIT(r.[Actions], ',') a CROSS APPLY STRING_SPLIT(r.[Subjects], ',') s)
	<> (SELECT COUNT(*) FROM @Seed)
	THROW 50001, 'Seed contains an unknown subject code.', 1;

IF EXISTS (SELECT 1 FROM @Seed s WHERE NOT EXISTS (SELECT 1 FROM [dbo].[ProjectModuleAction] x WHERE x.[ModuleCode] = s.[ModuleCode] AND x.[ActionCode] = s.[ActionCode]))
	THROW 50002, 'Seed contains a module/action that is not defined in ProjectModuleAction.', 1;

IF EXISTS (SELECT 1 FROM @Seed GROUP BY [SubjectType], [SubjectCode], [ModuleCode], [ActionCode] HAVING COUNT(*) > 1)
	THROW 50003, 'Seed contains duplicate subject/module/action rows.', 1;

INSERT INTO [dbo].[ProjectModulePermission] ([VendorId], [SubjectType], [SubjectCode], [ModuleCode], [ActionCode], [IsAllowed], [Scope], [IsActive], [IsDeleted], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate])
SELECT NULL, s.[SubjectType], s.[SubjectCode], s.[ModuleCode], s.[ActionCode], 1, 'ALL', 1, 0, 1, GETDATE(), 1, GETDATE()
FROM @Seed s
WHERE NOT EXISTS (SELECT 1 FROM [dbo].[ProjectModulePermission] p
				  WHERE p.[VendorId] IS NULL AND p.[SubjectType] = s.[SubjectType] AND p.[SubjectCode] = s.[SubjectCode]
				  AND p.[ModuleCode] = s.[ModuleCode] AND p.[ActionCode] = s.[ActionCode]);

DECLARE @Inserted INT = @@ROWCOUNT, @SeedCount INT = (SELECT COUNT(*) FROM @Seed);
PRINT CONCAT('ProjectModulePermission : ', @Inserted, ' system-default row(s) inserted (', @SeedCount, ' in seed).');

UPDATE [dbo].[PermissionVersion] SET [Version] = [Version] + 1 WHERE [Id] = 1;

INSERT INTO [dbo].[SchemaScriptLog] ([ScriptName], [AppliedOn]) VALUES (N'002_ProjectPermissions_Seed', SYSDATETIME());

COMMIT TRANSACTION;
PRINT '002_ProjectPermissions_Seed : applied.';
GO

SET NOEXEC OFF;
GO
