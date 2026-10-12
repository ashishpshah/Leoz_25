-- Tables for SQL Server distributed session (see Infra/DistributedSession.cs).
-- App creates these automatically on startup; run manually only if DB user has no CREATE TABLE permission.

IF OBJECT_ID(N'[dbo].[SessionCache]', N'U') IS NULL
BEGIN
	CREATE TABLE [dbo].[SessionCache] (
		[Id] NVARCHAR(449) COLLATE SQL_Latin1_General_CP1_CS_AS NOT NULL,
		[Value] VARBINARY(MAX) NOT NULL,
		[ExpiresAtTime] DATETIMEOFFSET NOT NULL,
		[SlidingExpirationInSeconds] BIGINT NULL,
		[AbsoluteExpiration] DATETIMEOFFSET NULL,
		CONSTRAINT [PK_SessionCache] PRIMARY KEY ([Id])
	);
	CREATE NONCLUSTERED INDEX [IX_SessionCache_ExpiresAtTime] ON [dbo].[SessionCache] ([ExpiresAtTime]);
END
GO

IF OBJECT_ID(N'[dbo].[DataProtectionKeys]', N'U') IS NULL
BEGIN
	CREATE TABLE [dbo].[DataProtectionKeys] (
		[Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
		[FriendlyName] NVARCHAR(200) NULL,
		[Xml] NVARCHAR(MAX) NOT NULL,
		[CreatedDate] DATETIME NOT NULL DEFAULT (GETDATE())
	);
END
GO
