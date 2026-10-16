using Leoz_25.Areas.Admin.Controllers;
using Leoz_25.Infra;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace Leoz_25.Tests;

/// <summary> Module Access screen (Super Admin only) against LeozApp_Test. Every test restores the state it changed. </summary>
[Collection(DatabaseCollection.Name)]
public class ModuleAccessControllerTests : IDisposable
{
	private readonly DataContext _db = TestDatabase.CreateContext();
	private readonly IRepositoryWrapper _context;
	private readonly PermissionService _service;

	public ModuleAccessControllerTests()
	{
		_context = TestDatabase.CreateRepository(_db);
		_service = new PermissionService(_context, new MemoryCache(new MemoryCacheOptions()));
	}

	public void Dispose() => _db.Dispose();

	/// <summary> Session of a logged-in user of the given role (1 Super Admin, 2 Admin, 3 Vendor, 4 Customer, 5 Employee). </summary>
	private ModuleAccessController Controller_As(int roleId)
	{
		TestDatabase.UseSession(new Dictionary<string, string>
		{
			[SessionKey.KEY_USER_ID] = (100 + roleId).ToString(),
			[SessionKey.KEY_USER_ROLE_ID] = roleId.ToString(),
			[SessionKey.KEY_IS_ADMIN] = roleId is 1 or 2 or 3 ? "1" : "0",   // Vendor role is flagged admin too
			[SessionKey.KEY_IS_VENDOR] = roleId == 3 ? "1" : "0",
			[SessionKey.KEY_IS_CUSTOMER] = roleId == 4 ? "1" : "0",
			[SessionKey.KEY_IS_EMPLOYEE] = roleId == 5 ? "1" : "0",
			[SessionKey.KEY_VENDOR_ID] = roleId is 3 or 4 or 5 ? "990001" : "0"
		});

		return new ModuleAccessController(_context, _service);
	}

	/// <summary> IsSuccess of ResponseModel or of an anonymous JSON object (internal to the app assembly, so read by reflection). </summary>
	private static bool IsSuccess(ActionResult result)
	{
		var value = ((JsonResult)result).Value!;

		return value.GetType().GetProperty("IsSuccess")?.GetValue(value) is true;
	}

	private string Allowed_Csv(string subjectCode)
		=> string.Join(",", _context.Using<ProjectModuleAction>().GetAll()
			.Where(x => _service.Build(PermissionSubjects.TypeOf(subjectCode), subjectCode, 0).Can(x.ModuleCode, x.ActionCode))
			.Select(x => x.ModuleCode + "." + x.ActionCode));

	private long Version() { _service.InvalidateVersionCache(); return _service.GetCurrentVersion(); }

	[Fact]
	public void SuperAdmin_changes_permission_with_audit_and_version()
	{
		var original = Allowed_Csv(ProjectAccess.COORDINATOR);
		var versionBefore = Version();
		var auditBefore = _context.Using<PermissionAudit>().GetAll().Count();

		try
		{
			Assert.True(IsSuccess(Controller_As(1).Save(ProjectAccess.COORDINATOR, original + ",WCOMP.ADD")));

			Assert.True(_service.Build(PermissionSubjects.DESIGNATION, ProjectAccess.COORDINATOR, 0).Can("WCOMP", "ADD"));
			Assert.Equal(versionBefore + 1, Version());
			Assert.Contains(_context.Using<PermissionAudit>().GetAll().Skip(auditBefore), x => x.VendorId == null && x.SubjectCode == ProjectAccess.COORDINATOR
				&& x.ModuleCode == "WCOMP" && x.ActionCode == "ADD" && x.OldValue == "Denied" && x.NewValue == "Allowed");
		}
		finally
		{
			Assert.True(IsSuccess(Controller_As(1).Save(ProjectAccess.COORDINATOR, original)));
		}

		Assert.False(_service.Build(PermissionSubjects.DESIGNATION, ProjectAccess.COORDINATOR, 0).Can("WCOMP", "ADD"));
		Assert.Equal(original, Allowed_Csv(ProjectAccess.COORDINATOR));
	}

	[Fact]
	public void SuperAdmin_can_open_the_screen_and_matrix()
	{
		Assert.IsType<ViewResult>(Controller_As(1).Index());
		Assert.True(IsSuccess(Controller_As(1).GetMatrix(PermissionSubjects.VENDOR)));
	}

	[Theory]
	[InlineData(2)] // Admin
	[InlineData(3)] // Vendor
	[InlineData(4)] // Customer
	[InlineData(5)] // Employee
	public void Other_roles_are_denied(int roleId)
	{
		var original = Allowed_Csv(ProjectAccess.SITE_ENGINEER);

		Assert.IsType<RedirectToActionResult>(Controller_As(roleId).Index());
		Assert.False(IsSuccess(Controller_As(roleId).GetMatrix(ProjectAccess.SITE_ENGINEER)));
		Assert.False(IsSuccess(Controller_As(roleId).Save(ProjectAccess.SITE_ENGINEER, original + ",DAILY.ADD")));

		Assert.Equal(original, Allowed_Csv(ProjectAccess.SITE_ENGINEER));
	}

	[Fact]
	public void Unknown_subject_is_rejected()
		=> Assert.False(IsSuccess(Controller_As(1).Save("ADMIN", "EPM.VIEW")));
}

[CollectionDefinition(Name, DisableParallelization = true)]
public class DatabaseCollection { public const string Name = "LeozApp_Test database"; }
