-- Project Detail module permissions : schema + system-default seed (see docs/permissions.md).
-- Idempotent : safe to run again. Existing permission rows are never overwritten (admin changes are kept).
-- Run BEFORE deploying code that maps LOV.DesignationCode. Then run ProjectPermissions_LovMigration.sql.

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
SET XACT_ABORT ON;
GO

-- =====================================================================================
-- Schema
-- =====================================================================================

IF OBJECT_ID(N'[dbo].[ProjectModule]', N'U') IS NULL
BEGIN
	CREATE TABLE [dbo].[ProjectModule] (
		[Code] VARCHAR(10) NOT NULL,
		[Name] NVARCHAR(100) NOT NULL,
		[DisplayOrder] INT NOT NULL,
		[IsActive] BIT NOT NULL CONSTRAINT [DF_ProjectModule_IsActive] DEFAULT (1),
		CONSTRAINT [PK_ProjectModule] PRIMARY KEY ([Code])
	);
END
GO

IF OBJECT_ID(N'[dbo].[PermissionAction]', N'U') IS NULL
BEGIN
	CREATE TABLE [dbo].[PermissionAction] (
		[Code] VARCHAR(20) NOT NULL,
		[Name] NVARCHAR(100) NOT NULL,
		CONSTRAINT [PK_PermissionAction] PRIMARY KEY ([Code])
	);
END
GO

-- Which actions exist for a module (drives the admin matrix columns and the permission FK)
IF OBJECT_ID(N'[dbo].[ProjectModuleAction]', N'U') IS NULL
BEGIN
	CREATE TABLE [dbo].[ProjectModuleAction] (
		[ModuleCode] VARCHAR(10) NOT NULL,
		[ActionCode] VARCHAR(20) NOT NULL,
		CONSTRAINT [PK_ProjectModuleAction] PRIMARY KEY ([ModuleCode], [ActionCode]),
		CONSTRAINT [FK_ProjectModuleAction_ProjectModule] FOREIGN KEY ([ModuleCode]) REFERENCES [dbo].[ProjectModule] ([Code]),
		CONSTRAINT [FK_ProjectModuleAction_PermissionAction] FOREIGN KEY ([ActionCode]) REFERENCES [dbo].[PermissionAction] ([Code])
	);
END
GO

-- VendorId NULL = system default, otherwise override for that vendor (tenant). Missing row = not allowed.
IF OBJECT_ID(N'[dbo].[ProjectModulePermission]', N'U') IS NULL
BEGIN
	CREATE TABLE [dbo].[ProjectModulePermission] (
		[Id] BIGINT IDENTITY(1,1) NOT NULL,
		[VendorId] BIGINT NULL,
		[SubjectType] VARCHAR(12) NOT NULL,
		[SubjectCode] VARCHAR(30) NOT NULL,
		[ModuleCode] VARCHAR(10) NOT NULL,
		[ActionCode] VARCHAR(20) NOT NULL,
		[IsAllowed] BIT NOT NULL,
		[Scope] VARCHAR(5) NOT NULL CONSTRAINT [DF_ProjectModulePermission_Scope] DEFAULT ('ALL'),
		[IsActive] BIT NOT NULL CONSTRAINT [DF_ProjectModulePermission_IsActive] DEFAULT (1),
		[IsDeleted] BIT NOT NULL CONSTRAINT [DF_ProjectModulePermission_IsDeleted] DEFAULT (0),
		[CreatedBy] BIGINT NOT NULL,
		[CreatedDate] DATETIME2 NULL,
		[LastModifiedBy] BIGINT NOT NULL,
		[LastModifiedDate] DATETIME2 NULL,
		[VendorKey] AS (ISNULL([VendorId], 0)) PERSISTED,
		CONSTRAINT [PK_ProjectModulePermission] PRIMARY KEY ([Id]),
		CONSTRAINT [CK_ProjectModulePermission_SubjectType] CHECK ([SubjectType] IN ('ACTOR', 'DESIGNATION')),
		CONSTRAINT [CK_ProjectModulePermission_Scope] CHECK ([Scope] IN ('ALL', 'OWN')),
		CONSTRAINT [FK_ProjectModulePermission_ProjectModuleAction] FOREIGN KEY ([ModuleCode], [ActionCode]) REFERENCES [dbo].[ProjectModuleAction] ([ModuleCode], [ActionCode])
	);

	CREATE UNIQUE NONCLUSTERED INDEX [UX_ProjectModulePermission_Subject]
		ON [dbo].[ProjectModulePermission] ([VendorKey], [SubjectType], [SubjectCode], [ModuleCode], [ActionCode])
		INCLUDE ([IsAllowed], [Scope], [IsActive], [IsDeleted]);
END
GO

-- Single row (Id = 1). Bumped on every permission change so logged-in users rebuild their permission set.
IF OBJECT_ID(N'[dbo].[PermissionVersion]', N'U') IS NULL
BEGIN
	CREATE TABLE [dbo].[PermissionVersion] (
		[Id] INT NOT NULL,
		[Version] BIGINT NOT NULL,
		CONSTRAINT [PK_PermissionVersion] PRIMARY KEY ([Id]),
		CONSTRAINT [CK_PermissionVersion_SingleRow] CHECK ([Id] = 1)
	);
END
GO

-- Canonical designation for LOV USER_TYPE rows (MANAGER, COORDINATOR, ...). Filled by ProjectPermissions_LovMigration.sql
IF COL_LENGTH(N'dbo.LOV', N'DesignationCode') IS NULL
	ALTER TABLE [dbo].[LOV] ADD [DesignationCode] VARCHAR(30) NULL;
GO

-- =====================================================================================
-- Reference data
-- =====================================================================================

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
GO

INSERT INTO [dbo].[PermissionAction] ([Code], [Name])
SELECT s.[Code], s.[Name]
FROM (VALUES
	('VIEW',        N'View'),
	('ADD',         N'Add'),
	('EDIT',        N'Edit'),
	('DELETE',      N'Delete'),
	('APPROVE',     N'Approve'),
	('REJECT',      N'Reject'),
	('COMPLETE',    N'Complete'),
	('ORDER',       N'Order'),
	('RECEIVE',     N'Receive'),
	('DELETE_FILE', N'Delete File')
) s ([Code], [Name])
WHERE NOT EXISTS (SELECT 1 FROM [dbo].[PermissionAction] a WHERE a.[Code] = s.[Code]);
GO

INSERT INTO [dbo].[ProjectModuleAction] ([ModuleCode], [ActionCode])
SELECT DISTINCT LTRIM(RTRIM(m.[value])), LTRIM(RTRIM(a.[value]))
FROM (VALUES
	('EPM,F3D,DRW,WCLTSC,MB', 'VIEW,ADD,EDIT,DELETE,APPROVE,REJECT'),
	('SITE',                  'VIEW,ADD,EDIT,DELETE,DELETE_FILE'),
	('WPEND',                 'VIEW,ADD,EDIT,DELETE,COMPLETE,REJECT'),
	('MPRO',                  'VIEW,ADD,EDIT,DELETE,ORDER,REJECT,RECEIVE'),
	('WCOMP',                 'VIEW,ADD,EDIT,DELETE,APPROVE,REJECT')
) s ([Modules], [Actions])
CROSS APPLY STRING_SPLIT(s.[Modules], ',') m
CROSS APPLY STRING_SPLIT(s.[Actions], ',') a
WHERE NOT EXISTS (SELECT 1 FROM [dbo].[ProjectModuleAction] x WHERE x.[ModuleCode] = LTRIM(RTRIM(m.[value])) AND x.[ActionCode] = LTRIM(RTRIM(a.[value])));
GO

IF NOT EXISTS (SELECT 1 FROM [dbo].[PermissionVersion] WHERE [Id] = 1)
	INSERT INTO [dbo].[PermissionVersion] ([Id], [Version]) VALUES (1, 1);
GO

-- =====================================================================================
-- System-default permissions (VendorId NULL) reproducing the legacy Infra/ProjectAccess.cs rules.
-- Only ALLOWED rows are seeded; anything not listed is denied.
-- Employee VIEW rows come from LOV.ProjectDetailTypeAccess (ProjectPermissions_LovMigration.sql).
-- =====================================================================================

BEGIN TRANSACTION;

DECLARE @Subject TABLE ([SubjectCode] VARCHAR(30) PRIMARY KEY, [SubjectType] VARCHAR(12) NOT NULL);

INSERT INTO @Subject ([SubjectCode], [SubjectType]) VALUES
	('VENDOR', 'ACTOR'), ('CUSTOMER', 'ACTOR'),
	('MANAGER', 'DESIGNATION'), ('COORDINATOR', 'DESIGNATION'), ('SITE_SUPERVISOR', 'DESIGNATION'), ('SITE_ENGINEER', 'DESIGNATION'),
	('PURCHASER', 'DESIGNATION'), ('ARCHITECT', 'DESIGNATION'), ('SITE_VENDOR', 'DESIGNATION'), ('OTHER', 'DESIGNATION');

DECLARE @Rule TABLE ([Modules] VARCHAR(200), [Actions] VARCHAR(200), [Subjects] VARCHAR(300), [Scope] VARCHAR(5));

INSERT INTO @Rule ([Modules], [Actions], [Subjects], [Scope]) VALUES
	-- Visibility : Vendor / Customer see all modules
	('EPM,F3D,DRW,WCLTSC,MB,SITE,WPEND,MPRO,WCOMP', 'VIEW', 'VENDOR,CUSTOMER', 'ALL'),

	-- Document modules (legacy CanUpload / CanModify / CanApproveDoc / CanAddClientScope)
	('EPM,F3D,DRW,MB',          'ADD',            'VENDOR,CUSTOMER,MANAGER,COORDINATOR,SITE_ENGINEER,ARCHITECT', 'ALL'),
	('WCLTSC',                  'ADD',            'VENDOR', 'ALL'),
	('EPM,WCLTSC',              'EDIT',           'VENDOR,CUSTOMER,MANAGER', 'ALL'),
	('F3D,DRW,MB',              'EDIT',           'VENDOR,CUSTOMER,MANAGER,COORDINATOR,SITE_ENGINEER,ARCHITECT', 'ALL'),
	('EPM,F3D,DRW,WCLTSC,MB',   'DELETE',         'VENDOR,CUSTOMER,MANAGER', 'ALL'),
	('EPM,F3D,DRW,WCLTSC,MB',   'APPROVE,REJECT', 'VENDOR,CUSTOMER,MANAGER,COORDINATOR', 'ALL'),

	-- Site Updates (Daily Update)
	('SITE', 'ADD,EDIT',    'COORDINATOR', 'ALL'),
	('SITE', 'DELETE',      'MANAGER', 'ALL'),
	('SITE', 'DELETE_FILE', 'MANAGER', 'ALL'),
	('SITE', 'DELETE_FILE', 'COORDINATOR', 'OWN'),

	-- Pending Selection & Work
	('WPEND', 'ADD',                    'VENDOR,MANAGER,COORDINATOR,SITE_ENGINEER,ARCHITECT', 'ALL'),
	('WPEND', 'EDIT',                   'VENDOR,MANAGER', 'ALL'),
	('WPEND', 'DELETE,COMPLETE,REJECT', 'VENDOR,CUSTOMER,MANAGER', 'ALL'),

	-- Material Procurement
	('MPRO', 'ADD',                      'VENDOR,CUSTOMER,MANAGER,COORDINATOR,SITE_SUPERVISOR,SITE_ENGINEER,SITE_VENDOR,OTHER', 'ALL'),
	('MPRO', 'EDIT,DELETE,ORDER,REJECT', 'MANAGER', 'ALL'),
	('MPRO', 'RECEIVE',                  'COORDINATOR', 'ALL'),

	-- Work Completion Report (APPROVE / REJECT : Customer only, intentional tightening of Save_Doc_Status "WC")
	('WCOMP', 'ADD,EDIT,DELETE', 'MANAGER', 'ALL'),
	('WCOMP', 'APPROVE,REJECT',  'CUSTOMER', 'ALL');

DECLARE @Seed TABLE ([SubjectType] VARCHAR(12), [SubjectCode] VARCHAR(30), [ModuleCode] VARCHAR(10), [ActionCode] VARCHAR(20), [Scope] VARCHAR(5));

INSERT INTO @Seed ([SubjectType], [SubjectCode], [ModuleCode], [ActionCode], [Scope])
SELECT sub.[SubjectType], sub.[SubjectCode], LTRIM(RTRIM(m.[value])), LTRIM(RTRIM(a.[value])), r.[Scope]
FROM @Rule r
CROSS APPLY STRING_SPLIT(r.[Modules], ',') m
CROSS APPLY STRING_SPLIT(r.[Actions], ',') a
CROSS APPLY STRING_SPLIT(r.[Subjects], ',') s
JOIN @Subject sub ON sub.[SubjectCode] = LTRIM(RTRIM(s.[value]));

-- Guard : every seeded (module, action) must exist in ProjectModuleAction, and no duplicates
IF EXISTS (SELECT 1 FROM @Seed s WHERE NOT EXISTS (SELECT 1 FROM [dbo].[ProjectModuleAction] x WHERE x.[ModuleCode] = s.[ModuleCode] AND x.[ActionCode] = s.[ActionCode]))
	THROW 50001, 'Seed contains a module/action that is not defined in ProjectModuleAction.', 1;

IF EXISTS (SELECT 1 FROM @Seed GROUP BY [SubjectType], [SubjectCode], [ModuleCode], [ActionCode] HAVING COUNT(*) > 1)
	THROW 50002, 'Seed contains duplicate subject/module/action rows.', 1;

INSERT INTO [dbo].[ProjectModulePermission] ([VendorId], [SubjectType], [SubjectCode], [ModuleCode], [ActionCode], [IsAllowed], [Scope], [IsActive], [IsDeleted], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate])
SELECT NULL, s.[SubjectType], s.[SubjectCode], s.[ModuleCode], s.[ActionCode], 1, s.[Scope], 1, 0, 1, GETDATE(), 1, GETDATE()
FROM @Seed s
WHERE NOT EXISTS (SELECT 1 FROM [dbo].[ProjectModulePermission] p
				  WHERE p.[VendorId] IS NULL AND p.[SubjectType] = s.[SubjectType] AND p.[SubjectCode] = s.[SubjectCode]
				  AND p.[ModuleCode] = s.[ModuleCode] AND p.[ActionCode] = s.[ActionCode]);

DECLARE @Inserted INT = @@ROWCOUNT, @SeedCount INT = (SELECT COUNT(*) FROM @Seed);
PRINT CONCAT('ProjectModulePermission : ', @Inserted, ' system-default row(s) inserted (', @SeedCount, ' in seed).');

UPDATE [dbo].[PermissionVersion] SET [Version] = [Version] + 1 WHERE [Id] = 1;

COMMIT TRANSACTION;
GO
