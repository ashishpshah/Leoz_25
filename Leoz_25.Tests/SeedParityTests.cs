using Leoz_25.Infra;
using Microsoft.Extensions.Caching.Memory;

namespace Leoz_25.Tests;

/// <summary>
/// The seeded system defaults in LeozApp_Test (scripts 001..003) reproduce the legacy rules for every
/// subject x module x action defined in ProjectModuleAction (documented exception : Save_Doc_Status "WC", see LegacyAccess).
/// </summary>
[Collection(DatabaseCollection.Name)]
public class SeedParityTests : IDisposable
{
	private readonly DataContext _db = TestDatabase.CreateContext();
	private readonly IRepositoryWrapper _context;

	public SeedParityTests() { _context = TestDatabase.CreateRepository(_db); }

	public void Dispose() => _db.Dispose();

	public static IEnumerable<object[]> Subjects
		=> PermissionSubjects.Actors.Concat(PermissionSubjects.Designations).Select(x => new object[] { x });

	private IReadOnlyDictionary<string, HashSet<string>> Get_Legacy_EmployeeView()
	{
		var modules = _context.Using<ProjectModule>().GetAll().Select(x => x.Code).ToList();

		return _context.Using<LOV>().GetByCondition(x => x.LOV_Column == "USER_TYPE").ToList()
			.GroupBy(x => ProjectAccess.ResolveDesignation(x.LOV_Code, x.LOV_Desc))
			.ToDictionary(g => g.Key, g => g.SelectMany(x => LegacyAccess.ParseModules(x.ProjectDetailTypeAccess, modules)).ToHashSet());
	}

	[Theory]
	[MemberData(nameof(Subjects))]
	public void Seed_equals_legacy_rules(string subjectCode)
	{
		var service = new PermissionService(_context, new MemoryCache(new MemoryCacheOptions()));
		var set = service.Build(PermissionSubjects.TypeOf(subjectCode), subjectCode, vendorId: 0);
		var employeeView = Get_Legacy_EmployeeView();

		var mismatches = _context.Using<ProjectModuleAction>().GetAll()
			.Where(x => set.Can(x.ModuleCode, x.ActionCode) != LegacyAccess.Can(subjectCode, x.ModuleCode, x.ActionCode, employeeView))
			.Select(x => $"{x.ModuleCode}.{x.ActionCode} : seed={set.Can(x.ModuleCode, x.ActionCode)}")
			.ToList();

		Assert.True(mismatches.Count == 0, $"{subjectCode} differs from legacy : {string.Join(", ", mismatches)}");
	}

	[Fact]
	public void Every_module_action_is_covered()
	{
		var list = _context.Using<ProjectModuleAction>().GetAll().ToList();

		// 5 document modules x 5 + SITE 3 + WPEND 5 + MPRO 6 + WCOMP 5
		Assert.Equal(44, list.Count);
		Assert.Equal(9, list.Select(x => x.ModuleCode).Distinct().Count());
	}

	[Fact]
	public void No_delete_permission_exists()
	{
		Assert.DoesNotContain(_context.Using<PermissionAction>().GetAll(), x => x.Code.Contains("DELETE"));
		Assert.DoesNotContain(_context.Using<ProjectModuleAction>().GetAll(), x => x.ActionCode.Contains("DELETE"));
		Assert.DoesNotContain(_context.Using<ProjectModulePermission>().GetAll(), x => x.ActionCode.Contains("DELETE"));
	}

	[Fact]
	public void Lov_designation_backfill_matches_ResolveDesignation()
	{
		foreach (var lov in _context.Using<LOV>().GetByCondition(x => x.LOV_Column == "USER_TYPE").ToList())
			Assert.Equal(ProjectAccess.ResolveDesignation(lov.LOV_Code, lov.LOV_Desc), lov.DesignationCode);
	}

	[Fact]
	public void Coordinator_designation_and_COORD_code_match()
	{
		// Save_DailyUpdate moved from UserType = "COORD" to designation COORDINATOR : must be the same LOV rows (report R7)
		foreach (var lov in _context.Using<LOV>().GetByCondition(x => x.LOV_Column == "USER_TYPE").ToList())
			Assert.Equal(lov.LOV_Code.Trim().ToUpper() == "COORD", lov.DesignationCode == ProjectAccess.COORDINATOR);
	}

	[Fact]
	public void Admin_without_subject_gets_empty_set()
	{
		var service = new PermissionService(_context, new MemoryCache(new MemoryCacheOptions()));

		Assert.Empty(service.Build(null, null, 0).Modules);
	}

	[Fact]
	public void Version_is_cached_until_invalidated()
	{
		var service = new PermissionService(_context, new MemoryCache(new MemoryCacheOptions()));

		TestDatabase.UseLoggedUser(1);

		using var transaction = _context.BeginTransaction();

		var before = service.GetCurrentVersion();

		service.BumpVersion();

		Assert.Equal(before, service.GetCurrentVersion());

		service.InvalidateVersionCache();

		Assert.Equal(before + 1, service.GetCurrentVersion());

		transaction.Rollback();
	}
}
