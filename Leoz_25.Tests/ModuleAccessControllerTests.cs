using Leoz_25.Areas.Admin.Controllers;
using Leoz_25.Infra;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace Leoz_25.Tests;

/// <summary> Module Access screen save / reset against LeozApp_Test. Every test restores the state it changed. </summary>
[Collection(DatabaseCollection.Name)]
public class ModuleAccessControllerTests : IDisposable
{
	private const long TEST_VENDOR_ID = 990001;

	private readonly DataContext _db = TestDatabase.CreateContext();
	private readonly IRepositoryWrapper _context;
	private readonly PermissionService _service;

	public ModuleAccessControllerTests()
	{
		_context = TestDatabase.CreateRepository(_db);
		_service = new PermissionService(_context, new MemoryCache(new MemoryCacheOptions()));
	}

	public void Dispose() => _db.Dispose();

	private ModuleAccessController Controller_As_SuperAdmin()
	{
		TestDatabase.UseSession(new Dictionary<string, string>
		{
			[SessionKey.KEY_USER_ID] = "1", [SessionKey.KEY_USER_ROLE_ID] = "1", [SessionKey.KEY_IS_ADMIN] = "1",
			[SessionKey.KEY_IS_VENDOR] = "0", [SessionKey.KEY_IS_CUSTOMER] = "0", [SessionKey.KEY_IS_EMPLOYEE] = "0"
		});

		return new ModuleAccessController(_context, _service);
	}

	private ModuleAccessController Controller_As_Vendor()
	{
		TestDatabase.UseSession(new Dictionary<string, string>
		{
			[SessionKey.KEY_USER_ID] = "2", [SessionKey.KEY_USER_ROLE_ID] = "3", [SessionKey.KEY_IS_ADMIN] = "1",
			[SessionKey.KEY_IS_VENDOR] = "1", [SessionKey.KEY_IS_CUSTOMER] = "0", [SessionKey.KEY_IS_EMPLOYEE] = "0",
			[SessionKey.KEY_VENDOR_ID] = TEST_VENDOR_ID.ToString()
		});

		return new ModuleAccessController(_context, _service);
	}

	private static bool IsSuccess(ActionResult result) => ((dynamic)((JsonResult)result).Value!).IsSuccess == true;

	private string Allowed_Csv(string subjectCode, long vendorId)
		=> string.Join(",", _context.Using<ProjectModuleAction>().GetAll()
			.Where(x => _service.Build(PermissionSubjects.TypeOf(subjectCode), subjectCode, vendorId).Can(x.ModuleCode, x.ActionCode))
			.Select(x => x.ModuleCode + "." + x.ActionCode));

	private long Version() { _service.InvalidateVersionCache(); return _service.GetCurrentVersion(); }

	[Fact]
	public void SuperAdmin_changes_system_default_with_audit_and_version()
	{
		var original = Allowed_Csv(ProjectAccess.COORDINATOR, 0);
		var versionBefore = Version();
		var auditBefore = _context.Using<PermissionAudit>().GetAll().Count();

		try
		{
			Assert.True(IsSuccess(Controller_As_SuperAdmin().Save(ProjectAccess.COORDINATOR, original + ",WCOMP.ADD")));

			Assert.True(_service.Build(PermissionSubjects.DESIGNATION, ProjectAccess.COORDINATOR, 0).Can("WCOMP", "ADD"));
			Assert.Equal(versionBefore + 1, Version());
			Assert.Contains(_context.Using<PermissionAudit>().GetAll().Skip(auditBefore), x => x.VendorId == null && x.SubjectCode == ProjectAccess.COORDINATOR
				&& x.ModuleCode == "WCOMP" && x.ActionCode == "ADD" && x.OldValue == "Denied" && x.NewValue == "Allowed");
		}
		finally
		{
			Assert.True(IsSuccess(Controller_As_SuperAdmin().Save(ProjectAccess.COORDINATOR, original)));
		}

		Assert.False(_service.Build(PermissionSubjects.DESIGNATION, ProjectAccess.COORDINATOR, 0).Can("WCOMP", "ADD"));
		Assert.Equal(original, Allowed_Csv(ProjectAccess.COORDINATOR, 0));
	}

	[Fact]
	public void Vendor_override_applies_to_own_tenant_only_and_reset_removes_it()
	{
		var original = Allowed_Csv(ProjectAccess.SITE_ENGINEER, TEST_VENDOR_ID);

		try
		{
			// Site Engineer may add daily updates in this vendor only, and loses Material Procurement ADD
			var changed = string.Join(",", original.Split(',').Where(x => x != "MPRO.ADD").Append("DAILY.ADD"));

			Assert.True(IsSuccess(Controller_As_Vendor().Save(ProjectAccess.SITE_ENGINEER, changed)));

			var vendorSet = _service.Build(PermissionSubjects.DESIGNATION, ProjectAccess.SITE_ENGINEER, TEST_VENDOR_ID);
			var defaultSet = _service.Build(PermissionSubjects.DESIGNATION, ProjectAccess.SITE_ENGINEER, 0);

			Assert.True(vendorSet.Can("DAILY", "ADD"));
			Assert.False(vendorSet.Can("MPRO", "ADD"));
			Assert.False(defaultSet.Can("DAILY", "ADD"));
			Assert.True(defaultSet.Can("MPRO", "ADD"));

			// Only differing cells are stored
			Assert.Equal(2, _context.Using<ProjectModulePermission>().GetByCondition(x => x.VendorId == TEST_VENDOR_ID).Count());
		}
		finally
		{
			Assert.True(IsSuccess(Controller_As_Vendor().ResetToDefault(ProjectAccess.SITE_ENGINEER)));
		}

		Assert.Empty(_context.Using<ProjectModulePermission>().GetByCondition(x => x.VendorId == TEST_VENDOR_ID));
		Assert.Equal(original, Allowed_Csv(ProjectAccess.SITE_ENGINEER, TEST_VENDOR_ID));
	}

	[Fact]
	public void Vendor_cannot_change_vendor_or_customer_rights()
	{
		Assert.False(IsSuccess(Controller_As_Vendor().Save(PermissionSubjects.VENDOR, "EPM.VIEW")));
		Assert.False(IsSuccess(Controller_As_Vendor().Save(PermissionSubjects.CUSTOMER, "EPM.VIEW")));
		Assert.Empty(_context.Using<ProjectModulePermission>().GetByCondition(x => x.VendorId == TEST_VENDOR_ID));
	}

	[Fact]
	public void Unknown_subject_is_rejected()
		=> Assert.False(IsSuccess(Controller_As_SuperAdmin().Save("ADMIN", "EPM.VIEW")));
}

[CollectionDefinition(Name, DisableParallelization = true)]
public class DatabaseCollection { public const string Name = "LeozApp_Test database"; }
