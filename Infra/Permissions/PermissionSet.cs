using System.Text.Json;

namespace Leoz_25.Infra
{
	/// <summary>
	/// Allowed Project Detail module actions of one logged-in user : module → action → scope (ALL / OWN).
	/// Missing module / action = not allowed. Kept as JSON in session (SessionKey.ProjectPermissions).
	/// </summary>
	public sealed class PermissionSet
	{
		public long Version { get; set; }

		public string SubjectType { get; set; } = "";

		public string SubjectCode { get; set; } = "";

		public Dictionary<string, Dictionary<string, string>> Modules { get; set; } = new();

		public static PermissionSet Empty(long version) => new PermissionSet() { Version = version };

		public bool Can(string module, string action) => Scope(module, action) != null;

		/// <summary> Scope of an allowed action (ALL / OWN), null when not allowed. </summary>
		public string? Scope(string module, string action)
		{
			if (string.IsNullOrWhiteSpace(module) || string.IsNullOrWhiteSpace(action)) return null;

			return Modules.TryGetValue(Normalize(module), out var actions) && actions.TryGetValue(Normalize(action), out var scope) ? scope : null;
		}

		public void Allow(string module, string action, string? scope)
		{
			module = Normalize(module);
			action = Normalize(action);

			if (!Modules.TryGetValue(module, out var actions)) Modules[module] = actions = new Dictionary<string, string>();

			actions[action] = Normalize(scope) == PermissionScopes.OWN ? PermissionScopes.OWN : PermissionScopes.ALL;
		}

		public string ToJson() => JsonSerializer.Serialize(this);

		public static PermissionSet? FromJson(string? json)
		{
			if (string.IsNullOrWhiteSpace(json)) return null;

			try { return JsonSerializer.Deserialize<PermissionSet>(json); }
			catch { return null; }
		}

		private static string Normalize(string? value) => (value ?? "").Trim().ToUpperInvariant();
	}
}
