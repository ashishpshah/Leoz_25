using Leoz_25.Controllers;
using Leoz_25.Infra;
using Microsoft.AspNetCore.Mvc;

namespace Leoz_25.Areas.Admin.Controllers
{
	/// <summary>
	/// Project Detail module permissions matrix (ProjectModulePermission).
	/// Super Admin / Admin : system defaults (VendorId NULL) of every subject.
	/// Vendor : overrides of Employee designations for own company (VendorId = own); Vendor / Customer rows are not editable.
	/// </summary>
	[Area("Admin")]
	public class ModuleAccessController : BaseController<ResponseModel<ProjectModulePermission>>
	{
		private readonly IPermissionService _permissionService;

		public ModuleAccessController(IRepositoryWrapper repository, IPermissionService permissionService) : base(repository) { _permissionService = permissionService; }

		/// <summary> Admin / Super Admin (Vendor role is also flagged admin, so excluded explicitly). </summary>
		private bool IsSystemEditor => Common.IsUserLogged() && (Common.IsSuperAdmin() || Common.IsAdmin()) && !IsVendor && !IsCustomer && !IsEmployee;

		private bool IsVendorEditor => IsVendor && Logged_In_VendorId > 0;

		/// <summary> Vendor edits its own overrides, Admin the system defaults. </summary>
		private long? EditVendorId => IsVendorEditor ? Logged_In_VendorId : null;

		private string[] EditableSubjects => IsSystemEditor ? PermissionSubjects.Actors.Concat(PermissionSubjects.Designations).ToArray()
			: (IsVendorEditor ? PermissionSubjects.Designations : Array.Empty<string>());

		public ActionResult Index()
		{
			if (!IsSystemEditor && !IsVendorEditor) return RedirectToAction("Index", "Home", new { area = "Admin" });

			CommonViewModel.SelectListItems = EditableSubjects.Select(x => new SelectListItem_Custom(x, PermissionSubjects.DisplayName(x), PermissionSubjects.TypeOf(x))).ToList();

			CommonViewModel.Data1 = IsVendorEditor;
			CommonViewModel.Data2 = ProjectAccess.Mode;

			return View(CommonViewModel);
		}

		[HttpPost]
		public ActionResult GetMatrix(string SubjectCode)
		{
			if (string.IsNullOrEmpty(SubjectCode) || !EditableSubjects.Contains(SubjectCode)) return Json_AccessDenied();

			var subjectType = PermissionSubjects.TypeOf(SubjectCode);

			var listModule = _context.Using<ProjectModule>().GetByCondition(x => x.IsActive).OrderBy(x => x.DisplayOrder).ToList();
			var listModuleAction = _context.Using<ProjectModuleAction>().GetAll().ToList();
			var listActionName = _context.Using<PermissionAction>().GetAll().ToList();
			var (defaults, overrides) = Get_Rows(subjectType, SubjectCode);

			var listAction = PermissionActions.All.Where(a => listModuleAction.Any(x => x.ActionCode == a)).ToList();

			var modules = listModule.Select(m => new
			{
				m.Code,
				m.Name,
				Actions = listAction.Select(a =>
				{
					var exists = listModuleAction.Any(x => x.ModuleCode == m.Code && x.ActionCode == a);
					var isDefault = Is_Allowed(defaults, m.Code, a);
					var objOverride = Get_Override(overrides, m.Code, a);

					return new
					{
						Code = a,
						Exists = exists,
						Allowed = exists && (objOverride != null ? objOverride.IsAllowed : isDefault),
						Default = exists && isDefault,
						IsOverride = exists && objOverride != null
					};
				}).ToList()
			}).ToList();

			return Json(new
			{
				IsSuccess = true,
				StatusCode = ResponseStatusCode.Success,
				SubjectCode,
				SubjectName = PermissionSubjects.DisplayName(SubjectCode),
				IsVendorScope = IsVendorEditor,
				HasOverrides = overrides.Any(x => x.IsActive && !x.IsDeleted),
				Actions = listAction.Select(a => new { Code = a, Name = listActionName.Where(x => x.Code == a).Select(x => x.Name).FirstOrDefault() ?? a }).ToList(),
				Modules = modules
			});
		}

		/// <summary> Allowed = comma separated MODULE.ACTION of all ticked cells for the subject. </summary>
		[HttpPost]
		public ActionResult Save(string SubjectCode, string Allowed)
		{
			try
			{
				if (string.IsNullOrEmpty(SubjectCode) || !EditableSubjects.Contains(SubjectCode)) return Json_AccessDenied();

				var subjectType = PermissionSubjects.TypeOf(SubjectCode);
				var vendorId = EditVendorId;

				var listModuleCode = _context.Using<ProjectModule>().GetByCondition(x => x.IsActive).Select(x => x.Code).ToList();
				var listModuleAction = _context.Using<ProjectModuleAction>().GetAll().Where(x => listModuleCode.Contains(x.ModuleCode)).ToList();

				var posted = (Allowed ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(x => x.ToUpper()).ToHashSet();

				var changes = 0;

				using (var transaction = _context.BeginTransaction())
				{
					try
					{
						var (defaults, overrides) = Get_Rows(subjectType, SubjectCode);

						foreach (var item in listModuleAction)
						{
							var isAllowed = posted.Contains(item.ModuleCode + "." + item.ActionCode);
							var isDefault = Is_Allowed(defaults, item.ModuleCode, item.ActionCode);

							if (vendorId == null)
							{
								// System default : upsert
								if (isAllowed == isDefault) continue;

								var obj = defaults.FirstOrDefault(x => x.ModuleCode == item.ModuleCode && x.ActionCode == item.ActionCode);

								if (obj != null) Update_Row(obj, isAllowed);
								else Add_Row(null, subjectType, SubjectCode, item.ModuleCode, item.ActionCode, isAllowed);

								Add_Audit(null, subjectType, SubjectCode, item.ModuleCode, item.ActionCode, isDefault, isAllowed);
								changes++;
							}
							else
							{
								// Vendor : keep an override row only where it differs from the system default
								var obj = overrides.FirstOrDefault(x => x.ModuleCode == item.ModuleCode && x.ActionCode == item.ActionCode);
								var objOverride = Get_Override(overrides, item.ModuleCode, item.ActionCode);
								var current = objOverride != null ? objOverride.IsAllowed : isDefault;

								if (isAllowed == current) continue;

								if (isAllowed == isDefault) { if (obj != null) _context.Using<ProjectModulePermission>().Delete(obj); }
								else if (obj != null) Update_Row(obj, isAllowed);
								else Add_Row(vendorId, subjectType, SubjectCode, item.ModuleCode, item.ActionCode, isAllowed);

								Add_Audit(vendorId, subjectType, SubjectCode, item.ModuleCode, item.ActionCode, current, isAllowed);
								changes++;
							}
						}

						if (changes > 0) _permissionService.BumpVersion();

						transaction.Commit();
					}
					catch (Exception ex) { LogService.LogInsert(GetCurrentAction(), "", ex); transaction.Rollback(); throw; }
				}

				if (changes > 0) Apply_Change(vendorId, SubjectCode, $"{changes} permission(s) changed");

				CommonViewModel.IsConfirm = true;
				CommonViewModel.IsSuccess = true;
				CommonViewModel.StatusCode = ResponseStatusCode.Success;
				CommonViewModel.Message = changes > 0 ? $"{changes} permission(s) saved successfully !" : "No change to save.";

				return Json(CommonViewModel);
			}
			catch (Exception ex) { LogService.LogInsert(GetCurrentAction(), "", ex); }

			CommonViewModel.IsSuccess = false;
			CommonViewModel.StatusCode = ResponseStatusCode.Error;
			CommonViewModel.Message = ResponseStatusMessage.Error;

			return Json(CommonViewModel);
		}

		/// <summary> Vendor : remove own override rows of the subject (back to system default). </summary>
		[HttpPost]
		public ActionResult ResetToDefault(string SubjectCode)
		{
			try
			{
				if (!IsVendorEditor || string.IsNullOrEmpty(SubjectCode) || !EditableSubjects.Contains(SubjectCode)) return Json_AccessDenied();

				var subjectType = PermissionSubjects.TypeOf(SubjectCode);
				var vendorId = EditVendorId;
				var changes = 0;

				using (var transaction = _context.BeginTransaction())
				{
					try
					{
						var (defaults, overrides) = Get_Rows(subjectType, SubjectCode);

						foreach (var obj in overrides)
						{
							var isDefault = Is_Allowed(defaults, obj.ModuleCode, obj.ActionCode);
							var isEffective = obj.IsActive && !obj.IsDeleted;

							_context.Using<ProjectModulePermission>().Delete(obj);

							if (isEffective && obj.IsAllowed != isDefault) Add_Audit(vendorId, subjectType, SubjectCode, obj.ModuleCode, obj.ActionCode, obj.IsAllowed, isDefault);
							changes++;
						}

						if (changes > 0) _permissionService.BumpVersion();

						transaction.Commit();
					}
					catch (Exception ex) { LogService.LogInsert(GetCurrentAction(), "", ex); transaction.Rollback(); throw; }
				}

				if (changes > 0) Apply_Change(vendorId, SubjectCode, $"reset to default ({changes} override(s) removed)");

				CommonViewModel.IsConfirm = true;
				CommonViewModel.IsSuccess = true;
				CommonViewModel.StatusCode = ResponseStatusCode.Success;
				CommonViewModel.Message = changes > 0 ? "Permissions reset to default successfully !" : "Already using default permissions.";

				return Json(CommonViewModel);
			}
			catch (Exception ex) { LogService.LogInsert(GetCurrentAction(), "", ex); }

			CommonViewModel.IsSuccess = false;
			CommonViewModel.StatusCode = ResponseStatusCode.Error;
			CommonViewModel.Message = ResponseStatusMessage.Error;

			return Json(CommonViewModel);
		}

		#region Helpers

		/// <summary> System default rows and (for a Vendor) own override rows of a subject. </summary>
		private (List<ProjectModulePermission> Defaults, List<ProjectModulePermission> Overrides) Get_Rows(string subjectType, string subjectCode)
		{
			var vendorId = EditVendorId;

			var rows = _context.Using<ProjectModulePermission>().GetByCondition(x => x.SubjectType == subjectType && x.SubjectCode == subjectCode
						&& (x.VendorId == null || (vendorId != null && x.VendorId == vendorId))).ToList();

			return (rows.Where(x => x.VendorId == null).ToList(), rows.Where(x => x.VendorId != null).ToList());
		}

		/// <summary> Active vendor override of a cell (inactive / soft-deleted rows do not override). </summary>
		private static ProjectModulePermission? Get_Override(List<ProjectModulePermission> overrides, string moduleCode, string actionCode)
			=> overrides.FirstOrDefault(x => x.ModuleCode == moduleCode && x.ActionCode == actionCode && x.IsActive && !x.IsDeleted);

		private static bool Is_Allowed(List<ProjectModulePermission> rows, string moduleCode, string actionCode)
			=> rows.Any(x => x.ModuleCode == moduleCode && x.ActionCode == actionCode && x.IsAllowed && x.IsActive && !x.IsDeleted);

		private void Add_Row(long? vendorId, string subjectType, string subjectCode, string moduleCode, string actionCode, bool isAllowed)
			=> _context.Using<ProjectModulePermission>().Add(new ProjectModulePermission()
			{
				VendorId = vendorId,
				SubjectType = subjectType,
				SubjectCode = subjectCode,
				ModuleCode = moduleCode,
				ActionCode = actionCode,
				IsAllowed = isAllowed,
				Scope = PermissionScopes.ALL,
				IsActive = true,
				IsDeleted = false,
				CreatedBy = Logged_In_UserId,
				CreatedDate = DateTime.Now,
				LastModifiedBy = Logged_In_UserId,
				LastModifiedDate = DateTime.Now
			});

		/// <summary> Scope of an existing row is kept. </summary>
		private void Update_Row(ProjectModulePermission obj, bool isAllowed)
		{
			obj.IsAllowed = isAllowed;
			obj.IsActive = true;
			obj.IsDeleted = false;
			obj.LastModifiedBy = Logged_In_UserId;
			obj.LastModifiedDate = DateTime.Now;

			_context.Using<ProjectModulePermission>().Update(obj);
		}

		private void Add_Audit(long? vendorId, string subjectType, string subjectCode, string moduleCode, string actionCode, bool oldValue, bool newValue)
			=> _context.Using<PermissionAudit>().Add(new PermissionAudit()
			{
				ChangedOn = DateTime.Now,
				ChangedBy = Logged_In_UserId,
				VendorId = vendorId,
				SubjectType = subjectType,
				SubjectCode = subjectCode,
				ModuleCode = moduleCode,
				ActionCode = actionCode,
				OldValue = oldValue ? "Allowed" : "Denied",
				NewValue = newValue ? "Allowed" : "Denied"
			});

		/// <summary> New PermissionVersion visible immediately on this server; logged-in users rebuild on their next request. </summary>
		private void Apply_Change(long? vendorId, string subjectCode, string message)
		{
			_permissionService.InvalidateVersionCache();

			LogService.LogInsert(GetCurrentAction(), $"Project module permissions : {(vendorId == null ? "system default" : "VendorId " + vendorId)} / {subjectCode} : {message} by UserId {Logged_In_UserId}.");
		}

		private JsonResult Json_AccessDenied()
		{
			CommonViewModel.IsSuccess = false;
			CommonViewModel.StatusCode = ResponseStatusCode.Error;
			CommonViewModel.Message = "You do not have access to perform this action.";

			return Json(CommonViewModel);
		}

		#endregion
	}
}
