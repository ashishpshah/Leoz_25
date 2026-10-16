using Leoz_25.Controllers;
using Leoz_25.Infra;
using Microsoft.AspNetCore.Mvc;

namespace Leoz_25.Areas.Admin.Controllers
{
	/// <summary>
	/// Project Detail module permissions matrix (ProjectModulePermission, system defaults : VendorId NULL).
	/// Super Admin only : every other role (Admin, Vendor, Customer, Employee) is redirected / denied.
	/// </summary>
	[Area("Admin")]
	public class ModuleAccessController : BaseController<ResponseModel<ProjectModulePermission>>
	{
		private readonly IPermissionService _permissionService;

		public ModuleAccessController(IRepositoryWrapper repository, IPermissionService permissionService) : base(repository) { _permissionService = permissionService; }

		/// <summary> Super Admin (role 1) only. </summary>
		private bool IsAllowedEditor => Common.IsUserLogged() && Common.IsSuperAdmin() && !IsVendor && !IsCustomer && !IsEmployee;

		private static string[] Subjects => PermissionSubjects.Actors.Concat(PermissionSubjects.Designations).ToArray();

		public ActionResult Index()
		{
			if (!IsAllowedEditor) return RedirectToAction("Index", "Home", new { area = "Admin" });

			CommonViewModel.SelectListItems = Subjects.Select(x => new SelectListItem_Custom(x, PermissionSubjects.DisplayName(x), PermissionSubjects.TypeOf(x))).ToList();

			CommonViewModel.Data2 = ProjectAccess.Mode;

			return View(CommonViewModel);
		}

		[HttpPost]
		public ActionResult GetMatrix(string SubjectCode)
		{
			if (!IsAllowedEditor || string.IsNullOrEmpty(SubjectCode) || !Subjects.Contains(SubjectCode)) return Json_AccessDenied();

			var subjectType = PermissionSubjects.TypeOf(SubjectCode);

			var listModule = _context.Using<ProjectModule>().GetByCondition(x => x.IsActive).OrderBy(x => x.DisplayOrder).ToList();
			var listModuleAction = _context.Using<ProjectModuleAction>().GetAll().ToList();
			var rows = Get_Rows(subjectType, SubjectCode);

			var listAction = PermissionActions.All.Where(a => listModuleAction.Any(x => x.ActionCode == a)).ToList();

			var modules = listModule.Select(m => new
			{
				m.Code,
				m.Name,
				Actions = listAction.Select(a =>
				{
					var exists = listModuleAction.Any(x => x.ModuleCode == m.Code && x.ActionCode == a);

					return new { Code = a, Exists = exists, Allowed = exists && Is_Allowed(rows, m.Code, a) };
				}).ToList()
			}).ToList();

			return Json(new
			{
				IsSuccess = true,
				StatusCode = ResponseStatusCode.Success,
				SubjectCode,
				SubjectName = PermissionSubjects.DisplayName(SubjectCode),
				Actions = listAction.Select(a => new { Code = a, Name = a.Substring(0, 1) + a.Substring(1).ToLower() }).ToList(),
				Modules = modules
			});
		}

		/// <summary> Allowed = comma separated MODULE.ACTION of all ticked cells for the subject. </summary>
		[HttpPost]
		public ActionResult Save(string SubjectCode, string Allowed)
		{
			try
			{
				if (!IsAllowedEditor || string.IsNullOrEmpty(SubjectCode) || !Subjects.Contains(SubjectCode)) return Json_AccessDenied();

				var subjectType = PermissionSubjects.TypeOf(SubjectCode);

				var listModuleCode = _context.Using<ProjectModule>().GetByCondition(x => x.IsActive).Select(x => x.Code).ToList();
				var listModuleAction = _context.Using<ProjectModuleAction>().GetAll().Where(x => listModuleCode.Contains(x.ModuleCode)).ToList();

				var posted = (Allowed ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(x => x.ToUpper()).ToHashSet();

				var changes = 0;

				using (var transaction = _context.BeginTransaction())
				{
					try
					{
						var rows = Get_Rows(subjectType, SubjectCode);

						foreach (var item in listModuleAction)
						{
							var isAllowed = posted.Contains(item.ModuleCode + "." + item.ActionCode);
							var current = Is_Allowed(rows, item.ModuleCode, item.ActionCode);

							if (isAllowed == current) continue;

							var obj = rows.FirstOrDefault(x => x.ModuleCode == item.ModuleCode && x.ActionCode == item.ActionCode);

							if (obj != null) Update_Row(obj, isAllowed);
							else Add_Row(subjectType, SubjectCode, item.ModuleCode, item.ActionCode, isAllowed);

							Add_Audit(subjectType, SubjectCode, item.ModuleCode, item.ActionCode, current, isAllowed);
							changes++;
						}

						if (changes > 0) _permissionService.BumpVersion();

						transaction.Commit();
					}
					catch (Exception ex) { LogService.LogInsert(GetCurrentAction(), "", ex); transaction.Rollback(); throw; }
				}

				if (changes > 0)
				{
					// New PermissionVersion visible immediately on this server; logged-in users rebuild on their next request
					_permissionService.InvalidateVersionCache();

					LogService.LogInsert(GetCurrentAction(), $"Project module permissions : {SubjectCode} : {changes} permission(s) changed by UserId {Logged_In_UserId}.");
				}

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

		#region Helpers

		/// <summary> System default rows (VendorId NULL) of a subject. </summary>
		private List<ProjectModulePermission> Get_Rows(string subjectType, string subjectCode)
			=> _context.Using<ProjectModulePermission>().GetByCondition(x => x.SubjectType == subjectType && x.SubjectCode == subjectCode && x.VendorId == null).ToList();

		private static bool Is_Allowed(List<ProjectModulePermission> rows, string moduleCode, string actionCode)
			=> rows.Any(x => x.ModuleCode == moduleCode && x.ActionCode == actionCode && x.IsAllowed && x.IsActive && !x.IsDeleted);

		private void Add_Row(string subjectType, string subjectCode, string moduleCode, string actionCode, bool isAllowed)
			=> _context.Using<ProjectModulePermission>().Add(new ProjectModulePermission()
			{
				VendorId = null,
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

		private void Add_Audit(string subjectType, string subjectCode, string moduleCode, string actionCode, bool oldValue, bool newValue)
			=> _context.Using<PermissionAudit>().Add(new PermissionAudit()
			{
				ChangedOn = DateTime.Now,
				ChangedBy = Logged_In_UserId,
				VendorId = null,
				SubjectType = subjectType,
				SubjectCode = subjectCode,
				ModuleCode = moduleCode,
				ActionCode = actionCode,
				OldValue = oldValue ? "Allowed" : "Denied",
				NewValue = newValue ? "Allowed" : "Denied"
			});

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
