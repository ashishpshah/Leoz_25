using Leoz_25.Infra;

namespace Leoz_25.Tests;

/// <summary> Merge rules of PermissionResolver / PermissionSet (no database). </summary>
public class PermissionResolverTests
{
	private const string DESIGNATION = PermissionSubjects.DESIGNATION;
	private const string COORD = ProjectAccess.COORDINATOR;

	private static ProjectModulePermission Row(long? vendorId, string module, string action, bool allowed = true, string scope = "ALL", bool active = true, bool deleted = false, string subject = COORD)
		=> new() { VendorId = vendorId, SubjectType = DESIGNATION, SubjectCode = subject, ModuleCode = module, ActionCode = action, IsAllowed = allowed, Scope = scope, IsActive = active, IsDeleted = deleted };

	private static PermissionSet Build(long vendorId, params ProjectModulePermission[] rows) => PermissionResolver.Build(rows, DESIGNATION, COORD, vendorId, 7);

	[Fact]
	public void Default_row_allows()
		=> Assert.True(Build(5, Row(null, "SITE", "ADD")).Can("SITE", "ADD"));

	[Fact]
	public void Missing_row_denies()
	{
		var set = Build(5, Row(null, "SITE", "ADD"));

		Assert.False(set.Can("SITE", "EDIT"));
		Assert.False(set.Can("MPRO", "ADD"));
	}

	[Fact]
	public void Denied_default_row_denies()
		=> Assert.False(Build(5, Row(null, "SITE", "ADD", allowed: false)).Can("SITE", "ADD"));

	[Fact]
	public void Vendor_override_deny_beats_default_allow()
		=> Assert.False(Build(5, Row(null, "SITE", "ADD"), Row(5, "SITE", "ADD", allowed: false)).Can("SITE", "ADD"));

	[Fact]
	public void Vendor_override_allow_beats_missing_default()
		=> Assert.True(Build(5, Row(5, "WCOMP", "ADD")).Can("WCOMP", "ADD"));

	[Fact]
	public void Other_vendor_override_is_ignored()
	{
		var set = Build(5, Row(null, "SITE", "ADD"), Row(9, "SITE", "ADD", allowed: false), Row(9, "WCOMP", "ADD"));

		Assert.True(set.Can("SITE", "ADD"));
		Assert.False(set.Can("WCOMP", "ADD"));
	}

	[Fact]
	public void Inactive_or_deleted_rows_are_ignored()
	{
		var set = Build(5,
			Row(null, "SITE", "ADD"), Row(5, "SITE", "ADD", allowed: false, active: false),
			Row(null, "SITE", "EDIT"), Row(5, "SITE", "EDIT", allowed: false, deleted: true),
			Row(null, "MPRO", "ADD", active: false));

		Assert.True(set.Can("SITE", "ADD"));
		Assert.True(set.Can("SITE", "EDIT"));
		Assert.False(set.Can("MPRO", "ADD"));
	}

	[Fact]
	public void Rows_of_other_subjects_are_ignored()
		=> Assert.False(Build(5, Row(null, "SITE", "ADD", subject: ProjectAccess.MANAGER)).Can("SITE", "ADD"));

	[Fact]
	public void Inactive_module_is_excluded()
	{
		var set = PermissionResolver.Build(new[] { Row(null, "SITE", "VIEW"), Row(null, "MB", "VIEW") }, DESIGNATION, COORD, 5, 1, new HashSet<string> { "SITE" });

		Assert.True(set.Can("SITE", "VIEW"));
		Assert.False(set.Can("MB", "VIEW"));
	}

	[Fact]
	public void Scope_own_is_kept()
	{
		var set = Build(5, Row(null, "SITE", "EDIT", scope: "OWN"), Row(null, "SITE", "ADD"));

		Assert.Equal(PermissionScopes.OWN, set.Scope("SITE", "EDIT"));
		Assert.Equal(PermissionScopes.ALL, set.Scope("SITE", "ADD"));
		Assert.Null(set.Scope("SITE", "VIEW"));
	}

	[Fact]
	public void Vendor_override_replaces_scope()
		=> Assert.Equal(PermissionScopes.OWN, Build(5, Row(null, "SITE", "EDIT"), Row(5, "SITE", "EDIT", scope: "OWN")).Scope("SITE", "EDIT"));

	[Fact]
	public void No_subject_gives_empty_set()
	{
		// Admin / Super Admin : no subject → nothing allowed (same as legacy HasModule)
		var set = PermissionResolver.Build(new[] { Row(null, "SITE", "VIEW") }, "", "", 0, 3);

		Assert.Empty(set.Modules);
		Assert.Equal(3, set.Version);
	}

	[Fact]
	public void Json_round_trip_keeps_rights_version_and_scope()
	{
		var set = Build(5, Row(null, "SITE", "EDIT", scope: "OWN"), Row(null, "EPM", "VIEW"));

		var copy = PermissionSet.FromJson(set.ToJson());

		Assert.NotNull(copy);
		Assert.Equal(7, copy!.Version);
		Assert.Equal(COORD, copy.SubjectCode);
		Assert.True(copy.Can("EPM", "VIEW"));
		Assert.Equal(PermissionScopes.OWN, copy.Scope("SITE", "EDIT"));
		Assert.False(copy.Can("EPM", "ADD"));
	}

	[Fact]
	public void Can_ignores_case_and_spaces_and_handles_null()
	{
		var set = Build(5, Row(null, "SITE", "ADD"));

		Assert.True(set.Can(" site ", "add"));
		Assert.False(set.Can(null!, "ADD"));
		Assert.False(set.Can("SITE", ""));
	}

	[Fact]
	public void Invalid_json_gives_null()
	{
		Assert.Null(PermissionSet.FromJson(null));
		Assert.Null(PermissionSet.FromJson("{not json"));
	}
}
