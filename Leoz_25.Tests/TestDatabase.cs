using Leoz_25.Infra;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Diagnostics.CodeAnalysis;

namespace Leoz_25.Tests;

/// <summary>
/// Test database LeozApp_Test on the local instance (create : Infra/Sql/run-local.ps1 -CreateTestDb -FromBackup ...).
/// Connection = the app's appsettings.json DataConnection (Windows authentication) with the catalog replaced in memory.
/// </summary>
internal static class TestDatabase
{
	public const string Catalog = "LeozApp_Test";

	private static readonly Lazy<string> _connectionString = new(() =>
	{
		var root = FindRepositoryRoot();

		var configuration = new ConfigurationBuilder().SetBasePath(root).AddJsonFile("appsettings.json", optional: false).Build();

		var builder = new SqlConnectionStringBuilder(configuration.GetConnectionString("DataConnection")) { InitialCatalog = Catalog };

		if (!builder.IntegratedSecurity) throw new InvalidOperationException("Tests run with Windows authentication only.");

		return builder.ConnectionString;
	});

	public static DataContext CreateContext()
	{
		UseSession(new Dictionary<string, string>());

		return new DataContext(new DbContextOptionsBuilder<DataContext>().UseSqlServer(_connectionString.Value).Options);
	}

	public static IRepositoryWrapper CreateRepository(DataContext context) => new RepositoryWrapper(context);

	/// <summary> AppHttpContextAccessor (static) with a request whose session holds the given values (DataContext.SaveChanges needs a logged-in user). </summary>
	public static void UseSession(Dictionary<string, string> values)
	{
		var httpContext = new DefaultHttpContext();
		var session = new TestSession();

		foreach (var item in values) session.SetString(item.Key, item.Value);

		httpContext.Features.Set<ISessionFeature>(new TestSessionFeature { Session = session });

		typeof(AppHttpContextAccessor).GetField("_httpContextAccessor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
			.SetValue(null, new HttpContextAccessor { HttpContext = httpContext });
	}

	public static void UseLoggedUser(long userId) => UseSession(new Dictionary<string, string> { [SessionKey.KEY_USER_ID] = userId.ToString() });

	private static string FindRepositoryRoot()
	{
		var dir = new DirectoryInfo(AppContext.BaseDirectory);

		while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Leoz_25.csproj"))) dir = dir.Parent;

		return dir?.FullName ?? throw new DirectoryNotFoundException("Leoz_25.csproj not found above the test output folder.");
	}

	private sealed class TestSessionFeature : ISessionFeature
	{
		public ISession Session { get; set; } = null!;
	}

	private sealed class TestSession : ISession
	{
		private readonly Dictionary<string, byte[]> _store = new();

		public bool IsAvailable => true;
		public string Id { get; } = Guid.NewGuid().ToString();
		public IEnumerable<string> Keys => _store.Keys;

		public void Clear() => _store.Clear();
		public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
		public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
		public void Remove(string key) => _store.Remove(key);
		public void Set(string key, byte[] value) => _store[key] = value;
		public bool TryGetValue(string key, [NotNullWhen(true)] out byte[]? value) => _store.TryGetValue(key, out value);
	}
}
