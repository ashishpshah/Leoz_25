-- 003 : one-time migration of LOV USER_TYPE rows to Project Detail permissions (see docs/permissions.md).
--   1. Backfill LOV.DesignationCode with the rules of the legacy ProjectAccess.ResolveDesignation (only where NULL).
--   2. Employee VIEW rights : LOV.ProjectDetailTypeAccess ("ALL" or CSV of module codes) -> ProjectModulePermission
--      (VendorId NULL, DESIGNATION / DesignationCode, VIEW). LOV rows mapping to the same designation are UNIONed.
--   3. Report (result sets R1..R7), including the COORD / COORDINATOR mismatch list (R7).
-- Re-runnable : skipped when already logged in SchemaScriptLog; existing DesignationCode values and permission rows are never overwritten.

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
DECLARE @Db SYSNAME = DB_NAME(), @Script NVARCHAR(128) = N'003_LOV_DesignationCode_Backfill';
IF @Db <> N'LeozApp' AND @Db <> ISNULL(CAST(SESSION_CONTEXT(N'Leoz.TargetDb') AS SYSNAME), N'')
BEGIN
	RAISERROR(N'%s : database "%s" is not LeozApp. Aborted.', 16, 1, @Script, @Db);
	SET NOEXEC ON;
END
ELSE IF OBJECT_ID(N'[dbo].[SchemaScriptLog]', N'U') IS NULL
	OR NOT EXISTS (SELECT 1 FROM [dbo].[SchemaScriptLog] WHERE [ScriptName] = N'002_ProjectPermissions_Seed')
BEGIN
	RAISERROR(N'%s : run 000, 001 and 002 first. Aborted.', 16, 1, @Script);
	SET NOEXEC ON;
END
GO

IF EXISTS (SELECT 1 FROM [dbo].[SchemaScriptLog] WHERE [ScriptName] = N'003_LOV_DesignationCode_Backfill')
BEGIN
	PRINT '003_LOV_DesignationCode_Backfill : already applied, skipped.';
	SET NOEXEC ON;
END
GO

BEGIN TRANSACTION;

-- Whitespace trimmed by C# string.Trim() for these values
DECLARE @ws NVARCHAR(10) = CONCAT(N' ', NCHAR(9), NCHAR(10), NCHAR(13));

-- -------------------------------------------------------------------------------------
-- 1. Backfill DesignationCode (mirror of ProjectAccess.ResolveDesignation, same order of checks :
--    code upper-cased; description lower-cased without "-" and " ")
-- -------------------------------------------------------------------------------------
UPDATE l
SET l.[DesignationCode] =
	CASE
		WHEN c.[Code] = N'MNGR'  OR c.[Descr] LIKE N'%manager%'     THEN 'MANAGER'
		WHEN c.[Code] = N'COORD' OR c.[Descr] LIKE N'%coordinator%' THEN 'COORDINATOR'
		WHEN c.[Code] = N'PUR'   OR c.[Descr] LIKE N'%purchase%'    THEN 'PURCHASER'
		WHEN c.[Descr] LIKE N'%supervisor%'                         THEN 'SITE_SUPERVISOR'
		WHEN c.[Descr] LIKE N'%engineer%'                           THEN 'SITE_ENGINEER'
		WHEN c.[Descr] LIKE N'%architect%'                          THEN 'ARCHITECT'
		WHEN c.[Descr] LIKE N'%vendor%'                             THEN 'SITE_VENDOR'
		ELSE 'OTHER'
	END
FROM [dbo].[LOV] l
CROSS APPLY (SELECT UPPER(TRIM(@ws FROM ISNULL(l.[LOV_Code], N''))) AS [Code],
					REPLACE(REPLACE(LOWER(TRIM(@ws FROM ISNULL(l.[LOV_Desc], N''))), N'-', N''), N' ', N'') AS [Descr]) c
WHERE l.[LOV_Column] = N'USER_TYPE' AND l.[DesignationCode] IS NULL;

PRINT CONCAT('LOV.DesignationCode backfilled : ', @@ROWCOUNT, ' row(s).');

-- -------------------------------------------------------------------------------------
-- 2. Employee VIEW rows (legacy HasModule : CSV split on ",", trimmed, case-sensitive match on module code or "ALL")
-- -------------------------------------------------------------------------------------
DECLARE @View TABLE ([DesignationCode] VARCHAR(30), [ModuleCode] VARCHAR(10), PRIMARY KEY ([DesignationCode], [ModuleCode]));

INSERT INTO @View ([DesignationCode], [ModuleCode])
SELECT DISTINCT l.[DesignationCode], m.[Code]
FROM [dbo].[LOV] l
CROSS APPLY STRING_SPLIT(ISNULL(l.[ProjectDetailTypeAccess], N''), N',') t
JOIN [dbo].[ProjectModule] m
	ON TRIM(@ws FROM t.[value]) COLLATE Latin1_General_CS_AS = N'ALL'
	OR TRIM(@ws FROM t.[value]) COLLATE Latin1_General_CS_AS = CAST(m.[Code] AS NVARCHAR(10)) COLLATE Latin1_General_CS_AS
WHERE l.[LOV_Column] = N'USER_TYPE' AND l.[DesignationCode] IS NOT NULL;

INSERT INTO [dbo].[ProjectModulePermission] ([VendorId], [SubjectType], [SubjectCode], [ModuleCode], [ActionCode], [IsAllowed], [Scope], [IsActive], [IsDeleted], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate])
SELECT NULL, 'DESIGNATION', v.[DesignationCode], v.[ModuleCode], 'VIEW', 1, 'ALL', 1, 0, 1, GETDATE(), 1, GETDATE()
FROM @View v
WHERE NOT EXISTS (SELECT 1 FROM [dbo].[ProjectModulePermission] p
				  WHERE p.[VendorId] IS NULL AND p.[SubjectType] = 'DESIGNATION' AND p.[SubjectCode] = v.[DesignationCode]
				  AND p.[ModuleCode] = v.[ModuleCode] AND p.[ActionCode] = 'VIEW');

PRINT CONCAT('ProjectModulePermission : ', @@ROWCOUNT, ' employee VIEW row(s) inserted.');

UPDATE [dbo].[PermissionVersion] SET [Version] = [Version] + 1 WHERE [Id] = 1;

INSERT INTO [dbo].[SchemaScriptLog] ([ScriptName], [AppliedOn]) VALUES (N'003_LOV_DesignationCode_Backfill', SYSDATETIME());

COMMIT TRANSACTION;
PRINT '003_LOV_DesignationCode_Backfill : applied.';
GO

-- =====================================================================================
-- Report (read-only)
-- =====================================================================================

-- R1. LOV USER_TYPE -> designation, old CSV
SELECT 'R1_LOV_DESIGNATION' AS [Report], l.[LOV_Code], l.[LOV_Desc], l.[DesignationCode], ISNULL(l.[ProjectDetailTypeAccess], N'') AS [Old_ProjectDetailTypeAccess]
FROM [dbo].[LOV] l
WHERE l.[LOV_Column] = N'USER_TYPE'
ORDER BY l.[DisplayOrder], l.[LOV_Code];

-- R2. Designations fed by more than one LOV row (their module lists were UNIONed - review)
SELECT 'R2_MERGED_DESIGNATIONS' AS [Report], l.[DesignationCode],
	STRING_AGG(CONCAT(l.[LOV_Code], N' [', ISNULL(l.[ProjectDetailTypeAccess], N''), N']'), N' | ') AS [Merged_LOV_Rows]
FROM [dbo].[LOV] l
WHERE l.[LOV_Column] = N'USER_TYPE'
GROUP BY l.[DesignationCode]
HAVING COUNT(*) > 1;

-- R3. Designation : old CSV(s) vs new VIEW rows (system default)
SELECT 'R3_VIEW_ROWS' AS [Report], d.[DesignationCode],
	(SELECT STRING_AGG(CONCAT(l.[LOV_Code], N'=', ISNULL(NULLIF(l.[ProjectDetailTypeAccess], N''), N'(empty)')), N' | ') FROM [dbo].[LOV] l WHERE l.[LOV_Column] = N'USER_TYPE' AND l.[DesignationCode] = d.[DesignationCode]) AS [Old_CSV],
	(SELECT STRING_AGG(p.[ModuleCode], ',') WITHIN GROUP (ORDER BY m.[DisplayOrder])
	 FROM [dbo].[ProjectModulePermission] p JOIN [dbo].[ProjectModule] m ON m.[Code] = p.[ModuleCode]
	 WHERE p.[VendorId] IS NULL AND p.[SubjectType] = 'DESIGNATION' AND p.[SubjectCode] = d.[DesignationCode]
	 AND p.[ActionCode] = 'VIEW' AND p.[IsAllowed] = 1 AND p.[IsActive] = 1 AND p.[IsDeleted] = 0) AS [New_VIEW_Modules]
FROM (VALUES ('MANAGER', 1), ('COORDINATOR', 2), ('SITE_SUPERVISOR', 3), ('SITE_ENGINEER', 4), ('PURCHASER', 5), ('ARCHITECT', 6), ('SITE_VENDOR', 7), ('OTHER', 8)) d ([DesignationCode], [Ord])
ORDER BY d.[Ord];

-- R4. CSV tokens that match no module (ignored by legacy HasModule too, e.g. wrong case / typo)
SELECT 'R4_IGNORED_TOKENS' AS [Report], l.[LOV_Code], x.[Token]
FROM [dbo].[LOV] l
CROSS APPLY STRING_SPLIT(ISNULL(l.[ProjectDetailTypeAccess], N''), N',') t
CROSS APPLY (SELECT TRIM(CONCAT(N' ', NCHAR(9), NCHAR(10), NCHAR(13)) FROM t.[value]) AS [Token]) x
WHERE l.[LOV_Column] = N'USER_TYPE'
AND x.[Token] <> N''
AND x.[Token] COLLATE Latin1_General_CS_AS <> N'ALL'
AND NOT EXISTS (SELECT 1 FROM [dbo].[ProjectModule] m WHERE CAST(m.[Code] AS NVARCHAR(10)) COLLATE Latin1_General_CS_AS = x.[Token] COLLATE Latin1_General_CS_AS);

-- R5. Duplicate USER_TYPE codes (login uses the first row found)
SELECT 'R5_DUPLICATE_CODES' AS [Report], l.[LOV_Code], COUNT(*) AS [Rows]
FROM [dbo].[LOV] l
WHERE l.[LOV_Column] = N'USER_TYPE'
GROUP BY l.[LOV_Code]
HAVING COUNT(*) > 1;

-- R6. Employees whose UserType has no LOV USER_TYPE row (no modules today, none after migration)
SELECT 'R6_EMPLOYEES_WITHOUT_LOV' AS [Report], e.[UserType], COUNT(*) AS [Employees]
FROM [dbo].[Employee] e
WHERE NOT EXISTS (SELECT 1 FROM [dbo].[LOV] l WHERE l.[LOV_Column] = N'USER_TYPE' AND l.[LOV_Code] = e.[UserType])
GROUP BY e.[UserType];

-- R7. COORD / COORDINATOR mismatches : today Save_DailyUpdate checks Employee.UserType = 'COORD',
--     Can("SITE", ADD/EDIT) checks designation COORDINATOR. Any row here changes who may save daily updates.
SELECT 'R7_COORD_MISMATCH' AS [Report], l.[LOV_Code], l.[LOV_Desc], l.[DesignationCode],
	CASE WHEN l.[LOV_Code] = N'COORD' THEN 'yes' ELSE 'no' END AS [Today_Can_Save_DailyUpdate],
	CASE WHEN l.[DesignationCode] = 'COORDINATOR' THEN 'yes' ELSE 'no' END AS [New_Can_Save_DailyUpdate],
	(SELECT COUNT(*) FROM [dbo].[Employee] e WHERE e.[UserType] = l.[LOV_Code]) AS [Employees],
	(SELECT COUNT(*) FROM [dbo].[Employee] e WHERE e.[UserType] = l.[LOV_Code] AND e.[IsActive] = 1) AS [Active_Employees]
FROM [dbo].[LOV] l
WHERE l.[LOV_Column] = N'USER_TYPE'
AND ((l.[DesignationCode] = 'COORDINATOR' AND l.[LOV_Code] <> N'COORD')
	OR (l.[LOV_Code] = N'COORD' AND ISNULL(l.[DesignationCode], '') <> 'COORDINATOR'));

IF @@ROWCOUNT = 0 PRINT 'R7_COORD_MISMATCH : none (every COORDINATOR row has code COORD and vice versa).';
GO

SET NOEXEC OFF;
GO
