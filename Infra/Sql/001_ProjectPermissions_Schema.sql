-- 001 : Project Detail module permissions - schema (see docs/permissions.md).
-- Re-runnable : skipped when already logged in SchemaScriptLog; every statement is also guarded (IF NOT EXISTS).
-- Scope : VIEW / ADD / EDIT and the workflow status actions. Delete is NOT a permission (unchanged legacy checks).

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

-- Safety : only LeozApp (or the database named in deploy_all.sql through SESSION_CONTEXT 'Leoz.TargetDb')
DECLARE @Db SYSNAME = DB_NAME(), @Script NVARCHAR(128) = N'001_ProjectPermissions_Schema';
IF @Db <> N'LeozApp' AND @Db <> ISNULL(CAST(SESSION_CONTEXT(N'Leoz.TargetDb') AS SYSNAME), N'')
BEGIN
	RAISERROR(N'%s : database "%s" is not LeozApp. Aborted.', 16, 1, @Script, @Db);
	SET NOEXEC ON;
END
ELSE IF OBJECT_ID(N'[dbo].[SchemaScriptLog]', N'U') IS NULL
BEGIN
	RAISERROR(N'%s : dbo.SchemaScriptLog is missing, run 000_SchemaScriptLog.sql first. Aborted.', 16, 1, @Script);
	SET NOEXEC ON;
END
GO

IF EXISTS (SELECT 1 FROM [dbo].[SchemaScriptLog] WHERE [ScriptName] = N'001_ProjectPermissions_Schema')
BEGIN
	PRINT '001_ProjectPermissions_Schema : already applied, skipped.';
	SET NOEXEC ON;
END
GO

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
-- IsDeleted is the EntitiesBase soft-delete flag of the row, not a delete permission.
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
END
GO

-- UNIQUE (ISNULL(VendorId,0), SubjectType, SubjectCode, ModuleCode, ActionCode) through the persisted VendorKey column
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [object_id] = OBJECT_ID(N'[dbo].[ProjectModulePermission]') AND [name] = N'UX_ProjectModulePermission_Subject')
	CREATE UNIQUE NONCLUSTERED INDEX [UX_ProjectModulePermission_Subject]
		ON [dbo].[ProjectModulePermission] ([VendorKey], [SubjectType], [SubjectCode], [ModuleCode], [ActionCode])
		INCLUDE ([IsAllowed], [Scope], [IsActive], [IsDeleted]);
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

IF NOT EXISTS (SELECT 1 FROM [dbo].[PermissionVersion] WHERE [Id] = 1)
	INSERT INTO [dbo].[PermissionVersion] ([Id], [Version]) VALUES (1, 1);
GO

-- One row per changed cell (admin matrix, reset to default)
IF OBJECT_ID(N'[dbo].[PermissionAudit]', N'U') IS NULL
BEGIN
	CREATE TABLE [dbo].[PermissionAudit] (
		[Id] BIGINT IDENTITY(1,1) NOT NULL,
		[ChangedOn] DATETIME2 NOT NULL CONSTRAINT [DF_PermissionAudit_ChangedOn] DEFAULT (SYSDATETIME()),
		[ChangedBy] BIGINT NOT NULL,
		[VendorId] BIGINT NULL,
		[SubjectType] VARCHAR(12) NOT NULL,
		[SubjectCode] VARCHAR(30) NOT NULL,
		[ModuleCode] VARCHAR(10) NOT NULL,
		[ActionCode] VARCHAR(20) NOT NULL,
		[OldValue] NVARCHAR(50) NULL,
		[NewValue] NVARCHAR(50) NULL,
		CONSTRAINT [PK_PermissionAudit] PRIMARY KEY ([Id])
	);

	CREATE NONCLUSTERED INDEX [IX_PermissionAudit_ChangedOn] ON [dbo].[PermissionAudit] ([ChangedOn] DESC);
END
GO

-- Canonical designation for LOV USER_TYPE rows (MANAGER, COORDINATOR, ...). Filled by 003_LOV_DesignationCode_Backfill.sql
IF COL_LENGTH(N'dbo.LOV', N'DesignationCode') IS NULL
	ALTER TABLE [dbo].[LOV] ADD [DesignationCode] VARCHAR(30) NULL;
GO

INSERT INTO [dbo].[SchemaScriptLog] ([ScriptName], [AppliedOn]) VALUES (N'001_ProjectPermissions_Schema', SYSDATETIME());
PRINT '001_ProjectPermissions_Schema : applied.';
GO

SET NOEXEC OFF;
GO
