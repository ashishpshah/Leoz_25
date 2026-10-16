using Leoz_25.Controllers;
using Leoz_25.Infra;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace Leoz_25.Areas.Admin.Controllers
{
	[Area("Admin")]
	public class EmployeeController : BaseController<ResponseModel<Employee>>
	{
		public EmployeeController(IRepositoryWrapper repository) : base(repository) { }

		// GET: Admin/Employee
		public ActionResult Index()
		{
			CommonViewModel.SelectListItems = new List<SelectListItem_Custom>();

			var listUOM = _context.Using<LOV>().GetByCondition(x => x.LOV_Column == "USER_TYPE").ToList();

			CommonViewModel.ObjList = DataContext_Command.Employee_Get(0, Logged_In_VendorId).ToList();

			var listInUseEmployeeId = Get_InUse_EmployeeIds(CommonViewModel.ObjList?.ToList());

			for (int i = 0; i < CommonViewModel.ObjList?.Count(); i++)
			{
				CommonViewModel.ObjList[i].UserType = listUOM.FirstOrDefault(x => x.LOV_Code == CommonViewModel.ObjList[i].UserType)?.LOV_Desc;
				CommonViewModel.ObjList[i].IsInUse = listInUseEmployeeId.Contains(CommonViewModel.ObjList[i].Id);
			}

			return View(CommonViewModel);
		}

		/// <summary>
		/// Edit Employee : protect projects which are already assigned / started.
		/// 1. Project where Employee is Coordinator can not be removed (change Coordinator in Project first), and Coordinator designation can not be changed.
		/// 2. Project where Employee has already worked can not be removed.
		/// 3. Employee assigned to an active Project can not be made In-Active.
		/// Returns error message, or null when valid.
		/// </summary>
		private string Get_ProjectAssignment_Error(Employee viewModel)
		{
			if (viewModel == null || viewModel.Id <= 0) return null;

			var objEmployee = _context.Using<Employee>().GetByCondition(x => x.Id == viewModel.Id && x.VendorId == Logged_In_VendorId).FirstOrDefault();

			if (objEmployee == null) return "Employee not found.";

			var dictProject = _context.Using<Project>().GetByCondition(x => x.VendorId == Logged_In_VendorId).ToDictionary(x => x.Id, x => x);

			string ProjectNames(IEnumerable<long> ids) => string.Join(", ", ids.Distinct().Select(x => dictProject.TryGetValue(x, out var p) ? p.Name : x.ToString()));

			var listNewProjectId = (viewModel.ProjectIds ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)
				.Select(x => long.TryParse(x.Trim(), out var id) ? id : 0).Where(x => x > 0).Distinct().ToList();

			var listOldProjectId = _context.Using<EmployeeProjectMapping>().GetByCondition(x => x.EmployeeId == objEmployee.Id).Select(x => x.ProjectId).Distinct().ToList();

			// 1. Coordinator of project(s)
			var listCoordinatorProjectId = dictProject.Values.Where(x => x.CoordinatorId == objEmployee.Id).Select(x => x.Id).ToList();

			if (listCoordinatorProjectId.Count > 0 && objEmployee.UserType == "COORD" && viewModel.UserType != "COORD")
				return $"Employee is Coordinator of Project(s) : {ProjectNames(listCoordinatorProjectId)}. Please change Coordinator in Project first, then change Designation.";

			var listRemovedCoordinatorProjectId = listCoordinatorProjectId.Where(x => !listNewProjectId.Contains(x)).ToList();

			if (listRemovedCoordinatorProjectId.Count > 0)
				return $"Employee is Coordinator of Project(s) : {ProjectNames(listRemovedCoordinatorProjectId)}. Please change Coordinator in Project first, then remove Project.";

			// 2. Already worked project(s)
			var listRemovedProjectId = listOldProjectId.Where(x => !listNewProjectId.Contains(x)).ToList();

			if (listRemovedProjectId.Count > 0 && objEmployee.UserId > 0)
			{
				var userId = objEmployee.UserId;

				var listWorkedProjectId = new List<long>();

				listWorkedProjectId.AddRange(_context.Using<ProjectDailyUpdate>().GetByCondition(x => listRemovedProjectId.Contains(x.ProjectId) && (x.CreatedBy == userId || x.LastModifiedBy == userId)).Select(x => x.ProjectId));
				listWorkedProjectId.AddRange(_context.Using<ProjectSiteDoc>().GetByCondition(x => listRemovedProjectId.Contains(x.ProjectId) && (x.CreatedBy == userId || x.LastModifiedBy == userId)).Select(x => x.ProjectId));
				listWorkedProjectId.AddRange(_context.Using<ProjectSiteMaterial>().GetByCondition(x => listRemovedProjectId.Contains(x.ProjectId) && (x.CreatedBy == userId || x.LastModifiedBy == userId)).Select(x => x.ProjectId));
				listWorkedProjectId.AddRange(_context.Using<ProjectSitePendingWork>().GetByCondition(x => listRemovedProjectId.Contains(x.ProjectId) && (x.CreatedBy == userId || x.LastModifiedBy == userId)).Select(x => x.ProjectId));
				listWorkedProjectId.AddRange(_context.Using<AgencyMaster>().GetByCondition(x => listRemovedProjectId.Contains(x.ProjectId) && (x.CreatedBy == userId || x.LastModifiedBy == userId)).Select(x => x.ProjectId));

				if (listWorkedProjectId.Count > 0)
					return $"Employee has already worked on Project(s) : {ProjectNames(listWorkedProjectId)}. Can not remove these Project(s).";
			}

			// 3. In-Active while assigned to active project(s)
			var listActiveAssignedProjectId = listNewProjectId.Where(x => dictProject.TryGetValue(x, out var p) && p.IsActive == true).ToList();

			if (objEmployee.IsActive == true && viewModel.IsActive == false && listActiveAssignedProjectId.Count > 0)
				return $"Employee is assigned to active Project(s) : {ProjectNames(listActiveAssignedProjectId)}. Please remove Project(s) / change Coordinator first, then make Employee In-Active.";

			return null;
		}

		/// <summary>
		/// A Project can have only one Coordinator. A Co-ordinator Employee can not be assigned to a Project
		/// which is already coordinated by another Employee (Project.CoordinatorId or Employee-Project mapping).
		/// Returns error message, or null when valid.
		/// </summary>
		private string Get_CoordinatorAssignment_Error(Employee viewModel)
		{
			if (viewModel == null || !string.Equals(viewModel.UserType, "COORD", StringComparison.OrdinalIgnoreCase)) return null;

			var listNewProjectId = (viewModel.ProjectIds ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)
				.Select(x => long.TryParse(x.Trim(), out var id) ? id : 0).Where(x => x > 0).Distinct().ToList();

			if (listNewProjectId.Count == 0) return null;

			var dictProject = _context.Using<Project>().GetByCondition(x => x.VendorId == Logged_In_VendorId && listNewProjectId.Contains(x.Id)).ToDictionary(x => x.Id, x => x);

			var listCoordinatorEmployee = _context.Using<Employee>().GetByCondition(x => x.VendorId == Logged_In_VendorId && x.UserType == "COORD").ToList();

			var dictMappedCoordinatorId = (from m in _context.Using<EmployeeProjectMapping>().GetByCondition(x => x.VendorId == Logged_In_VendorId && listNewProjectId.Contains(x.ProjectId)).ToList()
										   join e in listCoordinatorEmployee on m.EmployeeId equals e.Id
										   select new { m.ProjectId, EmployeeId = e.Id })
										   .GroupBy(x => x.ProjectId).ToDictionary(x => x.Key, x => x.Select(y => y.EmployeeId).ToList());

			foreach (var projectId in listNewProjectId)
			{
				var listCoordinatorId = new List<long>();

				if (dictProject.TryGetValue(projectId, out var objProject) && objProject.CoordinatorId > 0) listCoordinatorId.Add(objProject.CoordinatorId.Value);

				if (dictMappedCoordinatorId.TryGetValue(projectId, out var listMappedEmployeeId)) listCoordinatorId.AddRange(listMappedEmployeeId);

				listCoordinatorId = listCoordinatorId.Where(x => x != viewModel.Id).Distinct().ToList();

				if (listCoordinatorId.Count > 0)
				{
					var projectName = dictProject.TryGetValue(projectId, out var objP) ? objP.Name : projectId.ToString();
					var coordinatorName = string.Join(", ", listCoordinatorId.Select(x => listCoordinatorEmployee.Where(e => e.Id == x).Select(e => e.Fullname).FirstOrDefault()).Where(x => !string.IsNullOrEmpty(x)));

					return $"Project : {projectName} is already assigned to Coordinator : {coordinatorName}. Please remove the existing Coordinator from the Project first, then assign this Coordinator.";
				}
			}

			return null;
		}

		/// <summary>
		/// Employees (any designation) who are assigned to a project / coordinator of a project,
		/// or already started work on a project (Daily Update, Documents, Material, Pending Work, Work Completion).
		/// </summary>
		private HashSet<long> Get_InUse_EmployeeIds(List<Employee> listEmployee)
		{
			var result = new HashSet<long>();

			if (listEmployee == null || listEmployee.Count == 0) return result;

			var listEmployeeId = listEmployee.Select(x => x.Id).ToList();

			foreach (var id in _context.Using<EmployeeProjectMapping>().GetByCondition(x => listEmployeeId.Contains(x.EmployeeId)).Select(x => x.EmployeeId)) result.Add(id);

			foreach (var id in _context.Using<Project>().GetByCondition(x => x.VendorId == Logged_In_VendorId && x.CoordinatorId != null && listEmployeeId.Contains(x.CoordinatorId.Value)).Select(x => x.CoordinatorId.Value)) result.Add(id);

			// Work done is tracked by User Id (CreatedBy / LastModifiedBy)
			var dictUserEmployee = listEmployee.Where(x => x.UserId > 0).GroupBy(x => x.UserId).ToDictionary(x => x.Key, x => x.Select(y => y.Id).ToList());
			var listUserId = dictUserEmployee.Keys.ToList();

			if (listUserId.Count > 0)
			{
				var listWorkedUserId = new List<long>();

				listWorkedUserId.AddRange(_context.Using<ProjectDailyUpdate>().GetByCondition(x => listUserId.Contains(x.CreatedBy) || listUserId.Contains(x.LastModifiedBy)).SelectMany(x => new[] { x.CreatedBy, x.LastModifiedBy }));
				listWorkedUserId.AddRange(_context.Using<ProjectSiteDoc>().GetByCondition(x => listUserId.Contains(x.CreatedBy) || listUserId.Contains(x.LastModifiedBy)).SelectMany(x => new[] { x.CreatedBy, x.LastModifiedBy }));
				listWorkedUserId.AddRange(_context.Using<ProjectSiteMaterial>().GetByCondition(x => listUserId.Contains(x.CreatedBy) || listUserId.Contains(x.LastModifiedBy)).SelectMany(x => new[] { x.CreatedBy, x.LastModifiedBy }));
				listWorkedUserId.AddRange(_context.Using<ProjectSitePendingWork>().GetByCondition(x => listUserId.Contains(x.CreatedBy) || listUserId.Contains(x.LastModifiedBy)).SelectMany(x => new[] { x.CreatedBy, x.LastModifiedBy }));
				listWorkedUserId.AddRange(_context.Using<AgencyMaster>().GetByCondition(x => listUserId.Contains(x.CreatedBy) || listUserId.Contains(x.LastModifiedBy)).SelectMany(x => new[] { x.CreatedBy, x.LastModifiedBy }));

				foreach (var userId in listWorkedUserId.Distinct())
					if (dictUserEmployee.TryGetValue(userId, out var listId)) foreach (var id in listId) result.Add(id);
			}

			return result;
		}

		//[CustomAuthorizeAttribute(AccessType_Enum.Read)]
		public ActionResult Partial_AddEditForm(long Id = 0)
		{
			CommonViewModel.Obj = new Employee() { };

			if (Id > 0)
				//CommonViewModel.Obj = _context.Employees.GetByCondition(x => x.Id == Id).FirstOrDefault();
				CommonViewModel.Obj = DataContext_Command.Employee_Get(Id, Logged_In_VendorId).FirstOrDefault();

			if (CommonViewModel.Obj != null && CommonViewModel.Obj.UserId > 0)
			{
				var obj = _context.Using<User>().GetByCondition(x => x.Id == CommonViewModel.Obj.UserId).FirstOrDefault();

				if (obj != null && obj.IsActive == true && obj.IsDeleted == false)
					CommonViewModel.Obj.UserName = obj.UserName;
			}

			CommonViewModel.SelectListItems = new List<SelectListItem_Custom>();

			var listUOM = _context.Using<LOV>().GetByCondition(x => x.LOV_Column == "USER_TYPE").ToList();

			if (listUOM != null && listUOM.Count() > 0)
				CommonViewModel.SelectListItems.AddRange(listUOM.Select(x => new SelectListItem_Custom(x.LOV_Code, x.LOV_Desc, x.DisplayOrder, x.ProjectDetailTypeAccess, "UT")).ToList());

			// Active projects + already assigned (even In-Active) projects, so assigned project is not removed on save
			var listAssignedProjectId = CommonViewModel.Obj != null && CommonViewModel.Obj.Id > 0
				? _context.Using<EmployeeProjectMapping>().GetByCondition(x => x.EmployeeId == CommonViewModel.Obj.Id).Select(x => x.ProjectId).Distinct().ToList()
				: new List<long>();

			var listProject = _context.Using<Project>().GetByCondition(x => (x.IsActive == true || listAssignedProjectId.Contains(x.Id)) && x.VendorId == Logged_In_VendorId).Distinct().ToList();

			if (listProject != null && listProject.Count > 0)
				CommonViewModel.SelectListItems.AddRange(listProject.Select(x => new SelectListItem_Custom(x.Id.ToString(), x.Name, "P")).ToList());
			return PartialView("_Partial_AddEditForm", CommonViewModel);
		}

		[HttpPost]
		[AllowAnonymous]
		public JsonResult GetRole()
		{
			CommonViewModel.SelectListItems = new List<SelectListItem_Custom>();

			var listUOM = _context.Using<LOV>().GetByCondition(x => x.LOV_Column == "USER_TYPE").ToList();

			if (listUOM != null && listUOM.Count() > 0)
				CommonViewModel.SelectListItems.AddRange(listUOM.Select(x => new SelectListItem_Custom(x.LOV_Code, x.LOV_Desc, x.LOV_Column, x.DisplayOrder)).ToList());

			return Json(CommonViewModel.SelectListItems);
		}

		[HttpPost]
		//[CustomAuthorizeAttribute(AccessType_Enum.Write)]
		public ActionResult Save(Employee viewModel)
		{
            try
			{
                var subscriptionError = Get_Subscription_Error();

                if (!string.IsNullOrEmpty(subscriptionError))
                {
                    CommonViewModel.IsSuccess = false;
                    CommonViewModel.StatusCode = ResponseStatusCode.Error;
                    CommonViewModel.Message = subscriptionError;

                    return Json(CommonViewModel);
                }
                if (viewModel != null && viewModel != null)
				{
					#region Validation

					//if (!Common.IsAdmin())
					//{
					//	CommonViewModel.IsSuccess = false;
					//	CommonViewModel.StatusCode = ResponseStatusCode.Error;
					//	CommonViewModel.Message = ResponseStatusMessage.UnAuthorize;

					//	return Json(CommonViewModel);
					//}

					if (string.IsNullOrEmpty(viewModel.UserName))
					{
						CommonViewModel.IsSuccess = false;
						CommonViewModel.StatusCode = ResponseStatusCode.Error;
						CommonViewModel.Message = "Please Enter User Name.";

						return Json(CommonViewModel);
					}

					if (viewModel.Id == 0 && string.IsNullOrEmpty(viewModel.Password))
					{
						CommonViewModel.IsSuccess = false;
						CommonViewModel.StatusCode = ResponseStatusCode.Error;
						CommonViewModel.Message = "Please enter Password.";

						return Json(CommonViewModel);
					}

					if (string.IsNullOrEmpty(viewModel.FirstName))
					{
						CommonViewModel.IsSuccess = false;
						CommonViewModel.StatusCode = ResponseStatusCode.Error;
						CommonViewModel.Message = "Please Enter First Name.";

						return Json(CommonViewModel);
					}

					if (string.IsNullOrEmpty(viewModel.LastName))
					{
						CommonViewModel.IsSuccess = false;
						CommonViewModel.StatusCode = ResponseStatusCode.Error;
						CommonViewModel.Message = "Please Enter Last Name.";

						return Json(CommonViewModel);
					}

					if (string.IsNullOrEmpty(viewModel.UserType))
					{
						CommonViewModel.IsSuccess = false;
						CommonViewModel.StatusCode = ResponseStatusCode.Error;
						CommonViewModel.Message = "Please Select Designation.";

						return Json(CommonViewModel);
					}

					var coordinatorError = Get_CoordinatorAssignment_Error(viewModel);

					if (!string.IsNullOrEmpty(coordinatorError))
					{
						CommonViewModel.IsSuccess = false;
						CommonViewModel.StatusCode = ResponseStatusCode.Error;
						CommonViewModel.Message = coordinatorError;

						return Json(CommonViewModel);
					}

					var projectError = Get_ProjectAssignment_Error(viewModel);

					if (!string.IsNullOrEmpty(projectError))
					{
						CommonViewModel.IsSuccess = false;
						CommonViewModel.StatusCode = ResponseStatusCode.Error;
						CommonViewModel.Message = projectError;

						return Json(CommonViewModel);
					}

					#endregion

					#region Database-Transaction

					using (var transaction = _context.BeginTransaction())
					{
						try
						{
							if (!string.IsNullOrEmpty(viewModel.BirthDate_Text)) { try { viewModel.BirthDate = DateTime.ParseExact(viewModel.BirthDate_Text, "yyyy-MM-dd", CultureInfo.InvariantCulture); } catch { } }

							if (viewModel.IsPassword_Reset == true) viewModel.Password = "12345";

							if (!string.IsNullOrEmpty(viewModel.Password)) viewModel.Password = Common.Encrypt(viewModel.Password);

							viewModel.VendorId = Logged_In_VendorId;

							var (IsSuccess, response, Id) = DataContext_Command.Employee_Save(viewModel);
							viewModel.Id = Id;

							#region Coordinator-Project Mapping

							// A Co-ordinator Employee assigned to Project(s) becomes a Co-ordinator of those Project(s),
							// so the Project list shows the Co-ordinator Name(s) and the Project is treated as assigned.
							// Multiple Co-ordinators can be assigned to a Project; the first one becomes the primary
							// Co-ordinator (Project form) while the rest are shown from the Employee-Project mapping.
							if (IsSuccess && Id > 0 && string.Equals(viewModel.UserType, "COORD", StringComparison.OrdinalIgnoreCase))
							{
								var listCoordinatorProjectId = (viewModel.ProjectIds ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)
									.Select(x => long.TryParse(x.Trim(), out var projectId) ? projectId : 0).Where(x => x > 0).Distinct().ToList();

								foreach (var objProject in _context.Using<Project>().GetByCondition(x => listCoordinatorProjectId.Contains(x.Id) && x.VendorId == Logged_In_VendorId).ToList())
								{
									if (!(objProject.CoordinatorId > 0))
									{
										objProject.CoordinatorId = Id;
										_context.Using<Project>().Update(objProject);
									}

									// Co-ordinator sees a Project only through EmployeeProjectMapping, so keep it in sync
									if (!_context.Using<EmployeeProjectMapping>().Any(x => x.EmployeeId == Id && x.ProjectId == objProject.Id && x.VendorId == Logged_In_VendorId))
									{
										_context.Using<EmployeeProjectMapping>().Add(new EmployeeProjectMapping()
										{
											VendorId = Logged_In_VendorId,
											EmployeeId = Id,
											ProjectId = objProject.Id
										});
									}
								}
							}

							#endregion

							CommonViewModel.IsConfirm = IsSuccess;
							CommonViewModel.IsSuccess = IsSuccess;
							CommonViewModel.StatusCode = IsSuccess ? ResponseStatusCode.Success : ResponseStatusCode.Error;
							CommonViewModel.Message = response;
							CommonViewModel.RedirectURL = Url.Action("Index", "Employee", new { area = "Admin" });

							//Employee obj = null;

							////if (viewModel != null && !(viewModel.DisplayOrder > 0))
							////	viewModel.DisplayOrder = (_context.Companies.AsNoTracking().Max(x => x.DisplayOrder) ?? 0) + 1;

							//if (obj != null && Common.IsAdmin())
							//{
							//	obj.FirstName = viewModel.FirstName;
							//	obj.MiddleName = viewModel.MiddleName;
							//	obj.LastName = viewModel.LastName;
							//	obj.UserType = viewModel.UserType;
							//	obj.IsActive = (obj.Id == Common.LoggedUser_EmployeeId()) ? true : viewModel.IsActive;

							//	_context.Entry(obj).State = EntityState.Modified;
							//}
							//else if (Common.IsAdmin())
							//	_context.Employees.Add(viewModel);

							//_context.SaveChanges();


							//CommonViewModel.IsConfirm = true;
							//CommonViewModel.IsSuccess = true;
							//CommonViewModel.StatusCode = ResponseStatusCode.Success;
							//CommonViewModel.Message = ResponseStatusMessage.Success;
							//CommonViewModel.RedirectURL = Url.Action("Index", "Employee", new { area = "Admin" });

							transaction.Commit();

							return Json(CommonViewModel);
						}
						catch (Exception ex) { LogService.LogInsert(GetCurrentAction(), "", ex); transaction.Rollback(); }
					}

					#endregion
				}
			}
			catch (Exception ex) { LogService.LogInsert(GetCurrentAction(), "", ex); }

			CommonViewModel.Message = ResponseStatusMessage.Error;
			CommonViewModel.IsSuccess = false;
			CommonViewModel.StatusCode = ResponseStatusCode.Error;

			return Json(CommonViewModel);
		}

		[HttpPost]
		//[CustomAuthorizeAttribute(AccessType_Enum.Delete)]
		public ActionResult DeleteConfirmed(long Id)
		{
			try
			{
				//if (Common.IsAdmin() && !_context.Using<UserRoleMapping>().Any(x => x.EmployeeId == Id)
				//	&& _context.Employees.Any(x => x.Id > 1 && x.Id == Id))
				var objEmployee = _context.Using<Employee>().GetByCondition(x => x.Id == Id && x.VendorId == Logged_In_VendorId).FirstOrDefault();

				if (objEmployee == null)
				{
					CommonViewModel.IsSuccess = false;
					CommonViewModel.StatusCode = ResponseStatusCode.Error;
					CommonViewModel.Message = ResponseStatusMessage.Unable_Delete;

					return Json(CommonViewModel);
				}

				if (Get_InUse_EmployeeIds(new List<Employee>() { objEmployee }).Contains(objEmployee.Id))
				{
					CommonViewModel.IsSuccess = false;
					CommonViewModel.StatusCode = ResponseStatusCode.Error;
					CommonViewModel.Message = "Employee is assigned to Project / already worked on Project. Can not delete.";

					return Json(CommonViewModel);
				}

				if (true)
				{
					var (IsSuccess, response) = DataContext_Command.Employee_Status(Id, Logged_In_VendorId, false, true);

					CommonViewModel.IsConfirm = IsSuccess;
					CommonViewModel.IsSuccess = IsSuccess;
					CommonViewModel.StatusCode = IsSuccess ? ResponseStatusCode.Success : ResponseStatusCode.Error;
					CommonViewModel.Message = response;
					CommonViewModel.RedirectURL = Url.Action("Index", "Employee", new { area = "Admin" });

					//var obj = _context.Employees.GetByCondition(x => x.Id == Id).FirstOrDefault();

					//_context.Entry(obj).State = EntityState.Deleted;
					//_context.SaveChanges();

					//CommonViewModel.IsConfirm = true;
					//CommonViewModel.IsSuccess = true;
					//CommonViewModel.StatusCode = ResponseStatusCode.Success;
					//CommonViewModel.Message = ResponseStatusMessage.Delete;

					//CommonViewModel.RedirectURL = Url.Action("Index", "Employee", new { area = "Admin" });

					return Json(CommonViewModel);
				}
			}
			catch (Exception ex) { LogService.LogInsert(GetCurrentAction(), "", ex); }

			CommonViewModel.IsSuccess = false;
			CommonViewModel.StatusCode = ResponseStatusCode.Error;
			CommonViewModel.Message = ResponseStatusMessage.Unable_Delete;

			return Json(CommonViewModel);
		}


	}

}