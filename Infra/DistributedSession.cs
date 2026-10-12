using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Data.SqlClient;
using System.Xml.Linq;

namespace Leoz_25.Infra
{
	/// <summary>
	/// Session stored in SQL Server distributed cache (instead of In-Memory), so user is not logged out
	/// when IIS App Pool recycles / goes idle / site is re-published, and session is shared between multiple worker processes / servers.
	/// Data Protection keys (used to encrypt session cookie) are also stored in database, so old cookie remains valid after restart.
	/// </summary>
	public static class DistributedSession
	{
		public static IServiceCollection AddDistributedSqlServerSession(this IServiceCollection services, IConfiguration configuration)
		{
			var connectionString = configuration.GetConnectionString("DataConnection");

			var section = configuration.GetSection("DistributedSession");

			var schemaName = section.GetValue<string>("SchemaName") ?? "dbo";
			var tableName = section.GetValue<string>("TableName") ?? "SessionCache";
			var keyTableName = section.GetValue<string>("KeyTableName") ?? "DataProtectionKeys";
			var idleTimeoutMinutes = section.GetValue<int?>("IdleTimeoutMinutes") ?? 30;

			if (section.GetValue<bool?>("AutoCreateTables") ?? true)
				EnsureTables(connectionString, schemaName, tableName, keyTableName);

			services.AddDistributedSqlServerCache(options =>
			{
				options.ConnectionString = connectionString;
				options.SchemaName = schemaName;
				options.TableName = tableName;
				options.DefaultSlidingExpiration = TimeSpan.FromMinutes(idleTimeoutMinutes);
				options.ExpiredItemsDeletionInterval = TimeSpan.FromMinutes(30);
			});

			services.AddDataProtection()
				.SetApplicationName("Leoz_25")
				.AddKeyManagementOptions(options => options.XmlRepository = new SqlXmlRepository(connectionString, schemaName, keyTableName));

			services.AddSession(options =>
			{
				options.IdleTimeout = TimeSpan.FromMinutes(idleTimeoutMinutes);
				options.Cookie.Name = ".Leoz.Session";
				options.Cookie.HttpOnly = true;
				options.Cookie.IsEssential = true;
				options.Cookie.SameSite = SameSiteMode.Lax;
				options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
			});

			return services;
		}

		/// <summary> Create Session cache table and Data Protection key table if not exist. </summary>
		private static void EnsureTables(string connectionString, string schemaName, string tableName, string keyTableName)
		{
			var sql = $@"
IF OBJECT_ID(N'[{schemaName}].[{tableName}]', N'U') IS NULL
BEGIN
	CREATE TABLE [{schemaName}].[{tableName}] (
		[Id] NVARCHAR(449) COLLATE SQL_Latin1_General_CP1_CS_AS NOT NULL,
		[Value] VARBINARY(MAX) NOT NULL,
		[ExpiresAtTime] DATETIMEOFFSET NOT NULL,
		[SlidingExpirationInSeconds] BIGINT NULL,
		[AbsoluteExpiration] DATETIMEOFFSET NULL,
		CONSTRAINT [PK_{tableName}] PRIMARY KEY ([Id])
	);
	CREATE NONCLUSTERED INDEX [IX_{tableName}_ExpiresAtTime] ON [{schemaName}].[{tableName}] ([ExpiresAtTime]);
END

IF OBJECT_ID(N'[{schemaName}].[{keyTableName}]', N'U') IS NULL
BEGIN
	CREATE TABLE [{schemaName}].[{keyTableName}] (
		[Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
		[FriendlyName] NVARCHAR(200) NULL,
		[Xml] NVARCHAR(MAX) NOT NULL,
		[CreatedDate] DATETIME NOT NULL DEFAULT (GETDATE())
	);
END";

			try
			{
				var builder = new SqlConnectionStringBuilder(connectionString) { ConnectTimeout = 5 };
				using (var connection = new SqlConnection(builder.ConnectionString))
				using (var command = new SqlCommand(sql, connection))
				{
					command.CommandTimeout = 10;
					connection.Open();
					command.ExecuteNonQuery();
				}
			}
			catch (Exception ex)
			{
				// DB user may not have CREATE TABLE permission : run Infra/Sql/DistributedSession.sql manually
				Console.Error.WriteLine("DistributedSession : Unable to create session tables. " + ex.Message);
			}
		}
	}

	/// <summary> Store Data Protection keys in SQL Server. </summary>
	public class SqlXmlRepository : IXmlRepository
	{
		private readonly string _connectionString;
		private readonly string _tableName;

		public SqlXmlRepository(string connectionString, string schemaName, string tableName)
		{
			_connectionString = connectionString;
			_tableName = $"[{schemaName}].[{tableName}]";
		}

		public IReadOnlyCollection<XElement> GetAllElements()
		{
			var list = new List<XElement>();

			try
			{
				var builder = new SqlConnectionStringBuilder(_connectionString) { ConnectTimeout = 5 };
				using (var connection = new SqlConnection(builder.ConnectionString))
				using (var command = new SqlCommand($"SELECT [Xml] FROM {_tableName} ORDER BY [Id]", connection))
				{
					command.CommandTimeout = 10;
					connection.Open();

					using (var reader = command.ExecuteReader())
						while (reader.Read())
							if (!reader.IsDBNull(0)) list.Add(XElement.Parse(reader.GetString(0)));
				}
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine("SqlXmlRepository.GetAllElements error: " + ex.Message);
			}

			return list.AsReadOnly();
		}

		public void StoreElement(XElement element, string friendlyName)
		{
			try
			{
				var builder = new SqlConnectionStringBuilder(_connectionString) { ConnectTimeout = 5 };
				using (var connection = new SqlConnection(builder.ConnectionString))
				using (var command = new SqlCommand($"INSERT INTO {_tableName} ([FriendlyName], [Xml]) VALUES (@FriendlyName, @Xml)", connection))
				{
					command.CommandTimeout = 10;
					command.Parameters.AddWithValue("@FriendlyName", (object)friendlyName ?? DBNull.Value);
					command.Parameters.AddWithValue("@Xml", element.ToString(SaveOptions.DisableFormatting));

					connection.Open();
					command.ExecuteNonQuery();
				}
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine("SqlXmlRepository.StoreElement error: " + ex.Message);
			}
		}
	}
}
