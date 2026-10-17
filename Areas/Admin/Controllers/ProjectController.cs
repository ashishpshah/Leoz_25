using Leoz_25.Controllers;
using Leoz_25.Infra;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Build.Evaluation;
using Microsoft.CodeAnalysis;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;
using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Leoz_25.Areas.Admin.Controllers
{
	[Area("Admin")]
	public class ProjectController : BaseController<ResponseModel<Project>>
	{
		public ProjectController(IRepositoryWrapper repository) : base(repository) { }

		// GET: Admin/Project
		public ActionResult Index()
		{
			CommonViewModel.ObjList = _context.Using<Project>().GetByCondition(x => IsVendor == true ? x.VendorId == Logged_In_VendorId : false).ToList();

			var listEmployee = _context.Using<Employee>().GetByCondition(x => IsVendor == true ? x.VendorId == Logged_In_VendorId : false).Distinct().ToList();

			// A Project can have multiple Co-ordinators (Project form selection + Employee assignments), shown comma separated.
			var dictEmployee = listEmployee.GroupBy(x => x.Id).ToDictionary(x => x.Key, x => x.First().Fullname);

			var dictMappedCoordinatorId = (from m in _context.Using<EmployeeProjectMapping>().GetByCondition(x => IsVendor == true ? x.VendorId == Logged_In_VendorId : false).ToList()
										   join e in listEmployee on m.EmployeeId equals e.Id
										   where e.UserType == "COORD"
										   group e by m.ProjectId into g
										   select new { ProjectId = g.Key, EmployeeIds = g.Select(x => x.Id).ToList() })
										   .ToDictionary(x => x.ProjectId, x => x.EmployeeIds);

			if (listEmployee != null && listEmployee.Count > 0 && CommonViewModel.ObjList != null && CommonViewModel.ObjList.Count() > 0)
				foreach (var item in CommonViewModel.ObjList)
				{
					var listCoordinatorId = new List<long>();

					if (item.CoordinatorId > 0) listCoordinatorId.Add(item.CoordinatorId.Value);

					if (dictMappedCoordinatorId.TryGetValue(item.Id, out var listMappedEmployeeId)) listCoordinatorId.AddRange(listMappedEmployeeId);

					item.CoordinatorName = string.Join(", ", listCoordinatorId.Distinct()
						.Select(x => dictEmployee.TryGetValue(x, out var name) ? name : null)
						.Where(x => !string.IsNullOrEmpty(x)));
				}

			if (CommonViewModel.ObjList != null && CommonViewModel.ObjList.Count() > 0)
			{
				var listInUseProjectId = Get_InUse_ProjectIds(CommonViewModel.ObjList.Select(x => x.Id).ToList());

				foreach (var item in CommonViewModel.ObjList) item.IsInUse = listInUseProjectId.Contains(item.Id);
			}

			return View(CommonViewModel);
		}

		/// <summary>
		/// Projects which are assigned (Customer mapping / Employee mapping / Coordinator)
		/// or work is started (Documents, Document history, Material, Pending Work, Work Completion, Daily Update).
		/// </summary>
		private HashSet<long> Get_InUse_ProjectIds(List<long> listProjectId)
		{
			var result = new HashSet<long>();

			if (listProjectId == null || listProjectId.Count == 0) return result;

			result.UnionWith(_context.Using<Project>().GetByCondition(x => listProjectId.Contains(x.Id) && x.CoordinatorId != null && x.CoordinatorId > 0).Select(x => x.Id));
			result.UnionWith(_context.Using<CustomerProjectMapping>().GetByCondition(x => listProjectId.Contains(x.ProjectId)).Select(x => x.ProjectId));
			result.UnionWith(_context.Using<EmployeeProjectMapping>().GetByCondition(x => listProjectId.Contains(x.ProjectId)).Select(x => x.ProjectId));
			result.UnionWith(_context.Using<ProjectSiteDoc>().GetByCondition(x => listProjectId.Contains(x.ProjectId)).Select(x => x.ProjectId));
			result.UnionWith(_context.Using<ProjectSiteDoc_History>().GetByCondition(x => listProjectId.Contains(x.ProjectId)).Select(x => x.ProjectId));
			result.UnionWith(_context.Using<ProjectSiteMaterial>().GetByCondition(x => listProjectId.Contains(x.ProjectId)).Select(x => x.ProjectId));
			result.UnionWith(_context.Using<ProjectSitePendingWork>().GetByCondition(x => listProjectId.Contains(x.ProjectId)).Select(x => x.ProjectId));
			result.UnionWith(_context.Using<AgencyMaster>().GetByCondition(x => listProjectId.Contains(x.ProjectId)).Select(x => x.ProjectId));
			result.UnionWith(_context.Using<ProjectDailyUpdate>().GetByCondition(x => listProjectId.Contains(x.ProjectId)).Select(x => x.ProjectId));

			return result;
		}

		//[CustomAuthorizeAttribute(AccessType_Enum.Read)]
		public ActionResult Partial_AddEditForm(long Id = 0)
		{
			// same rule as Save : only a Vendor creates / edits a Project (the form is not given to other roles)
			if (!IsVendor || Logged_In_VendorId <= 0)
				return Content("<div class=\"alert alert-warning\">Only a Vendor can create or edit a Project.</div>", "text/html");

			CommonViewModel.Obj = new Project() { StartDate = DateTime.Now };

			if (Id > 0) CommonViewModel.Obj = _context.Using<Project>().GetByCondition(x => x.Id == Id && (IsVendor == true ? x.VendorId == Logged_In_VendorId : false)).FirstOrDefault();

			if (CommonViewModel.Obj == null) return Json(null);

			CommonViewModel.SelectListItems = new List<SelectListItem_Custom>();

			var listEmployee = _context.Using<Employee>().GetByCondition(x => (x.IsActive == true || x.Id == (CommonViewModel.Obj != null ? CommonViewModel.Obj.CoordinatorId : -1))
									&& (IsVendor == true ? x.VendorId == Logged_In_VendorId : false)
									&& x.UserType == "COORD").Distinct().ToList();

			if (listEmployee != null && listEmployee.Count > 0)
				CommonViewModel.SelectListItems.AddRange(listEmployee.Select(x => new SelectListItem_Custom(x.Id.ToString(), x.Fullname)).ToList());

			return PartialView("_Partial_AddEditForm", CommonViewModel);
		}

		[HttpPost]
		//[CustomAuthorizeAttribute(AccessType_Enum.Write)]
		public ActionResult Save(Project viewModel)
		{
			try
			{
				// Only Vendor can create / update Project
				if (!IsVendor || Logged_In_VendorId <= 0 || viewModel == null) return Json_AccessDenied();

				var subscriptionError = Get_Subscription_Error(IsNewProject: viewModel.Id <= 0);

				if (!string.IsNullOrEmpty(subscriptionError))
				{
					CommonViewModel.IsSuccess = false;
					CommonViewModel.StatusCode = ResponseStatusCode.Error;
					CommonViewModel.Message = subscriptionError;

					return Json(CommonViewModel);
				}

				if (viewModel.Id > 0 && !_context.Using<Project>().Any(x => x.Id == viewModel.Id && x.VendorId == Logged_In_VendorId)) return Json_AccessDenied();

				if (viewModel.CoordinatorId > 0 && !_context.Using<Employee>().Any(x => x.Id == viewModel.CoordinatorId && x.VendorId == Logged_In_VendorId && x.UserType == "COORD"))
				{
					CommonViewModel.IsSuccess = false;
					CommonViewModel.StatusCode = ResponseStatusCode.Error;
					CommonViewModel.Message = "Please select valid Coordinator.";

					return Json(CommonViewModel);
				}

				// A Project can have only one Coordinator. The existing Coordinator must be removed
				// (select "-- Select --") before assigning another one.
				if (viewModel.Id > 0 && viewModel.CoordinatorId > 0)
				{
					var objExistingProject = _context.Using<Project>().GetByCondition(x => x.Id == viewModel.Id && x.VendorId == Logged_In_VendorId).FirstOrDefault();

					var listCoordinatorEmployee = _context.Using<Employee>().GetByCondition(x => x.VendorId == Logged_In_VendorId && x.UserType == "COORD").ToList();

					var listExistingCoordinatorId = new List<long>();

					if (objExistingProject != null && objExistingProject.CoordinatorId > 0) listExistingCoordinatorId.Add(objExistingProject.CoordinatorId.Value);

					listExistingCoordinatorId.AddRange(from m in _context.Using<EmployeeProjectMapping>().GetByCondition(x => x.ProjectId == viewModel.Id && x.VendorId == Logged_In_VendorId).ToList()
													   join e in listCoordinatorEmployee on m.EmployeeId equals e.Id
													   select e.Id);

					listExistingCoordinatorId = listExistingCoordinatorId.Where(x => x != viewModel.CoordinatorId).Distinct().ToList();

					if (listExistingCoordinatorId.Count > 0)
					{
						var coordinatorName = string.Join(", ", listExistingCoordinatorId.Select(x => listCoordinatorEmployee.Where(e => e.Id == x).Select(e => e.Fullname).FirstOrDefault()).Where(x => !string.IsNullOrEmpty(x)));

						CommonViewModel.IsSuccess = false;
						CommonViewModel.StatusCode = ResponseStatusCode.Error;
						CommonViewModel.Message = $"Project is already assigned to Coordinator : {coordinatorName}. Please remove the existing Coordinator first, then assign another.";

						return Json(CommonViewModel);
					}
				}

				if (viewModel != null && viewModel != null)
				{
					#region Validation

					if (string.IsNullOrEmpty(viewModel.Name))
					{
						CommonViewModel.IsSuccess = false;
						CommonViewModel.StatusCode = ResponseStatusCode.Error;
						CommonViewModel.Message = "Please enter Project name.";

						return Json(CommonViewModel);
					}
                    DateTime startDate, handoverDate;

                    if (!string.IsNullOrEmpty(viewModel.StartDate_Text) &&
                        !string.IsNullOrEmpty(viewModel.HandoverDate_Text) &&
                        DateTime.TryParse(viewModel.StartDate_Text, out startDate) &&
                        DateTime.TryParse(viewModel.HandoverDate_Text, out handoverDate))
                    {
                        if (handoverDate < startDate)
                        {
                            CommonViewModel.IsSuccess = false;
                            CommonViewModel.StatusCode = ResponseStatusCode.Error;
                            CommonViewModel.Message = "Handover Date must be greater than or equal to Start Date.";

                            return Json(CommonViewModel);
                        }
                    }

                    if (viewModel.Id == 0 && string.IsNullOrEmpty(viewModel.StartDate_Text))
					{
						CommonViewModel.IsSuccess = false;
						CommonViewModel.StatusCode = ResponseStatusCode.Error;
						CommonViewModel.Message = "Please enter Start Date.";

						return Json(CommonViewModel);
					}

					if (_context.Using<Project>().GetByCondition(x => x.VendorId == Logged_In_VendorId && x.Id != viewModel.Id)
						.Any(x => x.Name.Trim().ToLower() == viewModel.Name.Trim().ToLower()))
					{
						CommonViewModel.Message = "Project already exist. Please try another Project name(s).";
						CommonViewModel.IsSuccess = false;
						CommonViewModel.StatusCode = ResponseStatusCode.Error;

						return Json(CommonViewModel);
					}

					#endregion

					#region Database-Transaction

					try
					{
						using (var transaction = _context.BeginTransaction())
						{
							try
							{
								if (!string.IsNullOrEmpty(viewModel.StartDate_Text)) { try { viewModel.StartDate = DateTime.ParseExact(viewModel.StartDate_Text, "yyyy-MM-dd", CultureInfo.InvariantCulture); } catch { } }
								if (!string.IsNullOrEmpty(viewModel.HandoverDate_Text)) { try { viewModel.HandoverDate = DateTime.ParseExact(viewModel.HandoverDate_Text, "yyyy-MM-dd", CultureInfo.InvariantCulture); } catch { } }

								Project obj = _context.Using<Project>().GetByCondition(x => x.Id == viewModel.Id).FirstOrDefault();

								long? oldCoordinatorId = obj?.CoordinatorId;

								if (obj != null)
								{
									obj.Name = viewModel.Name;
									obj.Description = viewModel.Description;
									obj.StartDate = viewModel.StartDate;
									obj.HandoverDate = viewModel.HandoverDate;
									obj.LocationLink = viewModel.LocationLink;
									obj.Address = viewModel.Address;
									obj.CityId = viewModel.CityId;
									obj.StateId = viewModel.StateId;
									obj.CountryId = viewModel.CountryId;
									obj.CoordinatorId = viewModel.CoordinatorId;
									obj.SiteDetails = viewModel.SiteDetails;
									obj.IsActive = viewModel.IsActive;

									obj.VendorId = Logged_In_VendorId;

									_context.Using<Project>().Update(obj);
								}
								else
								{
									viewModel.VendorId = Logged_In_VendorId;

									var _obj = _context.Using<Project>().Add(viewModel);
									viewModel.Id = _obj.Id;
								}

								#region Coordinator-Project Mapping

								// Coordinator sees a project only through EmployeeProjectMapping, so keep it in sync with the selected Coordinator
								// Old Coordinator keeps project if already worked on it (work history must stay with an assigned employee)
								var oldCoordinatorUserId = oldCoordinatorId > 0 ? _context.Using<Employee>().GetByCondition(x => x.Id == oldCoordinatorId).Select(x => x.UserId).FirstOrDefault() : 0;

								bool IsOldCoordinatorWorked = oldCoordinatorUserId > 0
									&& (_context.Using<ProjectDailyUpdate>().Any(x => x.ProjectId == viewModel.Id && (x.CreatedBy == oldCoordinatorUserId || x.LastModifiedBy == oldCoordinatorUserId))
										|| _context.Using<ProjectSiteDoc>().Any(x => x.ProjectId == viewModel.Id && (x.CreatedBy == oldCoordinatorUserId || x.LastModifiedBy == oldCoordinatorUserId))
										|| _context.Using<ProjectSiteMaterial>().Any(x => x.ProjectId == viewModel.Id && (x.CreatedBy == oldCoordinatorUserId || x.LastModifiedBy == oldCoordinatorUserId))
										|| _context.Using<ProjectSitePendingWork>().Any(x => x.ProjectId == viewModel.Id && (x.CreatedBy == oldCoordinatorUserId || x.LastModifiedBy == oldCoordinatorUserId))
										|| _context.Using<AgencyMaster>().Any(x => x.ProjectId == viewModel.Id && (x.CreatedBy == oldCoordinatorUserId || x.LastModifiedBy == oldCoordinatorUserId)));

								if (oldCoordinatorId > 0 && oldCoordinatorId != viewModel.CoordinatorId && !IsOldCoordinatorWorked)
								{
									var listOldMapping = _context.Using<EmployeeProjectMapping>().GetByCondition(x => x.EmployeeId == oldCoordinatorId && x.ProjectId == viewModel.Id && x.VendorId == Logged_In_VendorId).ToList();

									foreach (var mapping in listOldMapping) _context.Using<EmployeeProjectMapping>().Delete(mapping);
								}

								if (viewModel.CoordinatorId > 0
									&& _context.Using<Employee>().Any(x => x.Id == viewModel.CoordinatorId && x.VendorId == Logged_In_VendorId && x.UserType == "COORD")
									&& !_context.Using<EmployeeProjectMapping>().Any(x => x.EmployeeId == viewModel.CoordinatorId && x.ProjectId == viewModel.Id && x.VendorId == Logged_In_VendorId))
								{
									_context.Using<EmployeeProjectMapping>().Add(new EmployeeProjectMapping()
									{
										VendorId = Logged_In_VendorId,
										EmployeeId = viewModel.CoordinatorId ?? 0,
										ProjectId = viewModel.Id
									});
								}

								#endregion

								CommonViewModel.IsConfirm = true;
								CommonViewModel.IsSuccess = true;
								CommonViewModel.StatusCode = ResponseStatusCode.Success;
								CommonViewModel.Message = "Record saved successfully ! ";

								CommonViewModel.RedirectURL = Url.Action("Index", "Project", new { area = "Admin" });

								transaction.Commit();

								return Json(CommonViewModel);
							}
							catch (Exception ex) { LogService.LogInsert(GetCurrentAction(), "", ex); transaction.Rollback(); }
						}

					}
					catch (Exception ex) { LogService.LogInsert(GetCurrentAction(), "", ex); }

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
				var objProject = IsVendor ? _context.Using<Project>().GetByCondition(x => x.Id == Id && x.VendorId == Logged_In_VendorId).FirstOrDefault() : null;

				if (objProject != null && Get_InUse_ProjectIds(new List<long>() { objProject.Id }).Contains(objProject.Id))
				{
					CommonViewModel.IsSuccess = false;
					CommonViewModel.StatusCode = ResponseStatusCode.Error;
					CommonViewModel.Message = "Project is assigned to Customer / Employee or work is already started. Can not delete. Please make Project In-Active instead.";

					return Json(CommonViewModel);
				}

				if (objProject != null)
				{
					using (var transaction = _context.BeginTransaction())
					{
						foreach (var mapping in _context.Using<CustomerProjectMapping>().GetByCondition(x => x.ProjectId == Id).ToList()) _context.Using<CustomerProjectMapping>().Delete(mapping);
						foreach (var mapping in _context.Using<EmployeeProjectMapping>().GetByCondition(x => x.ProjectId == Id).ToList()) _context.Using<EmployeeProjectMapping>().Delete(mapping);

						_context.Using<Project>().Delete(objProject);

						transaction.Commit();
					}

					CommonViewModel.IsConfirm = true;
					CommonViewModel.IsSuccess = true;
					CommonViewModel.StatusCode = ResponseStatusCode.Success;
					CommonViewModel.Message = "Data deleted successfully ! ";

					CommonViewModel.RedirectURL = Url.Action("Index", "Project", new { area = "Admin" });

					return Json(CommonViewModel);
				}

			}
			catch (Exception ex) { LogService.LogInsert(GetCurrentAction(), "", ex); }

			CommonViewModel.IsSuccess = false;
			CommonViewModel.StatusCode = ResponseStatusCode.Error;
			CommonViewModel.Message = ResponseStatusMessage.Unable_Delete;

			return Json(CommonViewModel);
		}

		public ActionResult Partial_AddEditForm_Doc(long CustomerId = 0, long ProjectId = 0, string Type = null, long ProjectSiteDocId = 0)
		{
			CustomerId = (IsCustomer ? Logged_In_CustomerId : CustomerId);

			var _CommonViewModel = new ResponseModel<ProjectSiteDoc>() { Obj = new ProjectSiteDoc() { Type = Type, ProjectId = ProjectId, CustomerId = CustomerId, UploadDate = DateTime.Now.Date } };
            var _CommonViewModel1 = new ResponseModel<ProjectSiteDoc_History>();

			if (!IsProjectAllowed(ProjectId) || (!string.IsNullOrEmpty(Type) && !ProjectAccess.CanView(Type))) return Json(null);

            var objProject = (from x in _context.Using<CustomerProjectMapping>().GetByCondition(x => (IsCustomer ? x.CustomerId == CustomerId : true) && x.ProjectId == ProjectId && x.VendorId == Logged_In_VendorId).Distinct().ToList()
							  join z in _context.Using<Project>().GetByCondition(x => x.VendorId == Logged_In_VendorId).ToList() on x.ProjectId equals z.Id
							  where z.IsActive == true
							  select new Project
							  {
								  //VendorId = z.VendorId,
								  Name = z.Name,
								  Description = z.Description,
								  StartDate = z.StartDate,
								  HandoverDate = z.HandoverDate,
								  Address = z.Address,
								  CityId = z.CityId,
								  StateId = z.StateId,
								  CountryId = z.CountryId,
								  LocationLink = z.LocationLink,
								  CoordinatorId = z.CoordinatorId,
								  CoordinatorName = z.CoordinatorName,
								  SiteDetails = z.SiteDetails,
								  StartDate_Text = z.StartDate.ToString("dd/MM/yyyy").Replace("-", "/"),
								  HandoverDate_Text = z.HandoverDate.HasValue ? z.HandoverDate.Value.ToString("dd/MM/yyyy").Replace("-", "/") : string.Empty,
								  IsActive = z.IsActive
							  }).FirstOrDefault();

			if (objProject == null || (string.IsNullOrEmpty(Type) && ProjectSiteDocId == 0)) return Json(null);

			if (ProjectSiteDocId > 0)
			{
				//var obj = _context.Using<ProjectSiteDoc>().GetByCondition(x => x.Id == ProjectSiteDocId && x.ProjectId == ProjectId && x.CustomerId == CustomerId && x.IsActive == true && x.Type == Type).FirstOrDefault();

				var obj = _context.Using<ProjectSiteDoc>().GetByCondition(x => x.Id == ProjectSiteDocId && x.ProjectId == ProjectId && x.IsActive == true).FirstOrDefault();

				if (obj == null || !ProjectAccess.CanView(obj.Type)) return Json(null);

				// Approved / Rejected records can not be edited
				if (obj.Status == "A" || obj.Status == "R") return Json(null);

				// "UploadDate" input is datetime-local
				obj.UploadDate_Text = ((new[] { "F3D", "DRW", "MB" }).Contains(obj.Type) || obj.UploadDate == DateTime.MinValue ? DateTime.Now : obj.UploadDate).ToString("yyyy-MM-ddTHH:mm");

				// File data is not required in edit form
				return Json(new { obj.Id, obj.ProjectId, obj.CustomerId, obj.Type, obj.UploadDate_Text, obj.Remark });
			}
			else
			{
				// Customer access is checked by Customer-Project mapping (IsProjectAllowed), so all documents of project are shown
				// (not filtered by document CustomerId, which becomes stale when project is re-mapped to another customer)
				_CommonViewModel.ObjList = _context.Using<ProjectSiteDoc>().GetByCondition(x => x.ProjectId == ProjectId && x.IsActive == true && x.Type == Type).Distinct().ToList();
                
                _CommonViewModel1.ObjList = _context.Using<ProjectSiteDoc_History>().GetByCondition(x => x.ProjectId == ProjectId && x.IsActive == true && x.Type == Type && x.IsLatest != true).Distinct().ToList();

                if (_CommonViewModel.ObjList != null || _CommonViewModel.ObjList.Count() > 0)
				{
					foreach (var item in _CommonViewModel.ObjList)
					{
						item.Status_Text = item.Status == "U" ? "Uploaded" : (item.Status == "A" ? "Approved" : (item.Status == "R" ? "Rejected" : ""));
						if (item.StatusDate != null) item.StatusDate_Text = item.StatusDate.ToString(Common.DateTimeFormat_ddMMyyyy);
					}
				}
                if (_CommonViewModel1.ObjList != null || _CommonViewModel1.ObjList.Count() > 0)
                {
                    foreach (var item in _CommonViewModel1.ObjList)
                    {
                        item.Status_Text = item.Status == "U" ? "Uploaded" : (item.Status == "A" ? "Approved" : (item.Status == "R" ? "Rejected" : ""));
                        if (item.StatusDate != null) item.StatusDate_Text = item.StatusDate.ToString(Common.DateTimeFormat_ddMMyyyy);
                    }
                }
                return PartialView("_Partial_AddEditForm_Doc", Tuple.Create(_CommonViewModel, _CommonViewModel1));
            }
		}

		[HttpPost]
		public ActionResult Save_Doc(ProjectSiteDoc viewModel)
		{
			try
			{
				if (viewModel != null)
				{
					#region Validation

					if (viewModel.ProjectId <= 0)
					{
						CommonViewModel.IsSuccess = false;
						CommonViewModel.StatusCode = ResponseStatusCode.Error;
						CommonViewModel.Message = "Please select Project.";

						return Json(CommonViewModel);
					}

					if (string.IsNullOrEmpty(viewModel.Type))
					{
						CommonViewModel.IsSuccess = false;
						CommonViewModel.StatusCode = ResponseStatusCode.Error;
						CommonViewModel.Message = "Please select valid type.";

						return Json(CommonViewModel);
					}

					if (viewModel.UploadDate == null)
					{
						CommonViewModel.IsSuccess = false;
						CommonViewModel.StatusCode = ResponseStatusCode.Error;
						CommonViewModel.Message = "Please enter Upload Date.";

						return Json(CommonViewModel);
					}

					var files = AppHttpContextAccessor.AppHttpContext.Request.Form.Files;
                    bool isFileUploaded = files != null && files.Any(f => f.Length > 0);
                    var allowedTypes = new[] { "F3D", "DRW", "MB" };
                    bool isHistoryType = allowedTypes.Contains(viewModel.Type);
                    if ( viewModel.Id == 0 && (files == null || files.Count() <= 0))
					{
						CommonViewModel.IsSuccess = false;
						CommonViewModel.StatusCode = ResponseStatusCode.Error;
						CommonViewModel.Message = "Please Upload Documents.";

						return Json(CommonViewModel);
					}
					string[] allowedExtensions = { ".png", ".jpg", ".jpeg", ".pdf" };
                    string[] allowedMimeTypes = { "image/png", "image/jpeg", "application/pdf" };

                    foreach (var file in files)
                    {
                        if (file.Length > 0)
                        {
                            var extension = Path.GetExtension(file.FileName).ToLower();
                            var mimeType = file.ContentType.ToLower();

                            // Check extension
                            if (!allowedExtensions.Contains(extension))
                            {
                                CommonViewModel.IsSuccess = false;
                                CommonViewModel.StatusCode = ResponseStatusCode.Error;
                                CommonViewModel.Message = "Invalid file type.Only PNG, JPG, JPEG, and PDF files are allowed.";
                                return Json(CommonViewModel);
                            }

                            // Check MIME type
                            if (!allowedMimeTypes.Contains(mimeType))
                            {
                                CommonViewModel.IsSuccess = false;
                                CommonViewModel.StatusCode = ResponseStatusCode.Error;
                                CommonViewModel.Message = "Invalid file type. Only PNG, JPG, JPEG, and PDF are allowed.";
                                return Json(CommonViewModel);
                            }
                        }
                    }


                    if (string.IsNullOrEmpty(viewModel.Remark))
					{
						CommonViewModel.IsSuccess = false;
						CommonViewModel.StatusCode = ResponseStatusCode.Error;
						CommonViewModel.Message = "Please enter Remarks.";

						return Json(CommonViewModel);
					}

					// Legacy : Upload : Manager, Co-ordinator, Site Engineer, Architect. Edit existing record : Manager (F3D / DRW / MB always keep latest version, so upload rights are enough)
					bool legacyCanSave = ProjectAccess.CanUpload
						&& !(viewModel.Id > 0 && !isHistoryType && !ProjectAccess.CanModify)
						&& !(viewModel.Id == 0 && viewModel.Type == "WCLTSC" && !ProjectAccess.CanAddClientScope);

					if (!IsProjectAllowed(viewModel.ProjectId) || !ProjectAccess.CanView(viewModel.Type)
						|| !ProjectAccess.Check(legacyCanSave, viewModel.Type, viewModel.Id == 0 ? PermissionActions.ADD : PermissionActions.EDIT)) return Json_AccessDenied();

					if (viewModel.Id > 0 && !_context.Using<ProjectSiteDoc>().Any(x => x.Id == viewModel.Id && x.ProjectId == viewModel.ProjectId)) return Json_AccessDenied();

					// Approved / Rejected records can not be edited
					if (viewModel.Id > 0 && _context.Using<ProjectSiteDoc>().Any(x => x.Id == viewModel.Id && (x.Status == "A" || x.Status == "R")))
					{
						CommonViewModel.IsSuccess = false;
						CommonViewModel.StatusCode = ResponseStatusCode.Error;
						CommonViewModel.Message = "Approved / Rejected record can not be edited.";
						return Json(CommonViewModel);
					}

					#endregion

					#region Database-Transaction

					using (var transaction = _context.BeginTransaction())
					{
						try
						{
							long HistoryId = 0;

                            //if (!string.IsNullOrEmpty(viewModel.UploadDate_Text)) { try { viewModel.UploadDate = DateTime.ParseExact(viewModel.UploadDate_Text, "yyyy-MM-dd", CultureInfo.InvariantCulture); } catch { } }

							ProjectSiteDoc obj = _context.Using<ProjectSiteDoc>().GetByCondition(x => x.Id == viewModel.Id).FirstOrDefault();

							if ((new[] { "F3D", "DRW", "MB" }).Contains(viewModel.Type)) obj = _context.Using<ProjectSiteDoc>().GetByCondition(x => x.ProjectId == viewModel.ProjectId && x.Type == viewModel.Type).OrderByDescending(x => x.Id).FirstOrDefault();

							if (obj != null && obj.Status != "R")
							{
								obj.Remark = viewModel.Remark;
								obj.UploadDate = viewModel.UploadDate;
								//obj.FilePath = !string.IsNullOrEmpty(viewModel.FilePath) ? viewModel.FilePath : obj.FilePath;
								obj.Type = viewModel.Type;
								_context.Using<ProjectSiteDoc>().Update(obj);
                                var oldHistory = new List<ProjectSiteDoc_History>();
                                if (isHistoryType)
								{
                                    oldHistory = _context.Using<ProjectSiteDoc_History>()
                                   .GetByCondition(x => x.ProjectId == obj.ProjectId && x.Type == obj.Type && x.IsLatest == true)
                               .ToList();
                                }
                               
                                
                                // ✅ Insert history ONLY if file uploaded
                                if (isFileUploaded && isHistoryType)
                                {
                                    foreach (var item in oldHistory)
                                    {
                                        item.IsLatest = false;
                                        _context.Using<ProjectSiteDoc_History>().Update(item);
                                    }
                                    var objHistory = new ProjectSiteDoc_History()
                                    {
                                        Status = obj.Status,
										StatusDate = obj.StatusDate,
										Type = obj.Type,
                                        ProjectId = obj.ProjectId,
                                        CustomerId = obj.CustomerId,
                                        UploadDate = obj.UploadDate,
                                        Remark = obj.Remark,
										IsLatest = true
                                    };

                                    var _objHistory =  _context.Using<ProjectSiteDoc_History>().Add(objHistory);
                                    HistoryId = _objHistory.Id;
                                }
                                viewModel.Id = obj.Id;
							}
							else if (obj != null && obj.Status == "R")
							{
								CommonViewModel.IsSuccess = false;
								CommonViewModel.StatusCode = ResponseStatusCode.Error;
								CommonViewModel.Message = "Record was rejected. Can not allow to update. ";

								return Json(CommonViewModel);
							}
							else
							{
								viewModel.Status = "U";
								viewModel.StatusDate = DateTime.Now;

								viewModel.CustomerId = (IsCustomer ? Logged_In_CustomerId : viewModel.CustomerId);
								var _obj = _context.Using<ProjectSiteDoc>().Add(viewModel);
                                if (isHistoryType)
                                {
                                    var objHistory = new ProjectSiteDoc_History()
                                    {
                                        Status = viewModel.Status,
                                        StatusDate = viewModel.StatusDate,
                                        ProjectId = viewModel.ProjectId,
                                        CustomerId = viewModel.CustomerId,
                                        UploadDate = viewModel.UploadDate,
                                        Remark = viewModel.Remark,
                                        Type = viewModel.Type,
                                        IsLatest = true
                                    };

                                    var _objHistory = _context.Using<ProjectSiteDoc_History>().Add(objHistory);
                                    HistoryId = _objHistory.Id;
                                }
        //                        var objHistory = new ProjectSiteDoc_History();
								//objHistory.Status = viewModel.Status;
								//objHistory.StatusDate = viewModel.StatusDate;
								//objHistory.ProjectId = viewModel.ProjectId;
								//objHistory.CustomerId = viewModel.CustomerId;
								//objHistory.UploadDate = viewModel.UploadDate;
								//objHistory.Remark = viewModel.Remark;
								//objHistory.Type = viewModel.Type;
								//objHistory.IsLatest = true;
        //                        var _objHistory = _context.Using<ProjectSiteDoc_History>().Add(objHistory);
								viewModel.Id = _obj.Id;
								//HistoryId = _objHistory.Id;
							}

							CommonViewModel.IsConfirm = true;
							CommonViewModel.IsSuccess = true;
							CommonViewModel.StatusCode = ResponseStatusCode.Success;
							CommonViewModel.Message = "Record saved successfully ! ";

							try
							{
								if (files != null && files.Count() > 0 && files[0].Length > 0)
								{
									var file = files[0];

									using (var memoryStream = new MemoryStream())
									{
										file.CopyTo(memoryStream);

										obj = _context.Using<ProjectSiteDoc>().GetByCondition(x => x.Id == viewModel.Id).FirstOrDefault();

                                        var historyObj = _context.Using<ProjectSiteDoc_History>()
										.GetByCondition(x => x.Id == HistoryId)
										.FirstOrDefault();

                                        if (obj != null && memoryStream.ToArray().Length > 0)
										{
											obj.FileName = Path.GetFileName(file.FileName);
											obj.FileContentType = file.ContentType;
											obj.FileData = memoryStream.ToArray();                                           

                                            _context.Using<ProjectSiteDoc>().Update(obj);
                                            
                                        }
                                        if (historyObj != null && memoryStream.ToArray().Length > 0)
                                        {
                                            historyObj.FileName = Path.GetFileName(file.FileName);
                                            historyObj.FileContentType = file.ContentType;
                                            historyObj.FileData = memoryStream.ToArray();

                                            _context.Using<ProjectSiteDoc_History>().Update(historyObj);
                                        }
                                    }
								}
							}
							catch (Exception ex)
							{
								LogService.LogInsert(GetCurrentAction(), "", ex);
								//CommonViewModel.Message = "Issue in Uploading Image/PDF.";
								//CommonViewModel.IsSuccess = false;
								//CommonViewModel.StatusCode = ResponseStatusCode.Error;
							}

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
		public ActionResult DeleteConfirmed_Doc(long Id )
		{
			try
			{
				var objProject = _context.Using<ProjectSiteDoc>().GetByCondition(x => x.Id == Id).FirstOrDefault();

				if (objProject == null || !ProjectAccess.CanModify || !IsProjectAllowed(objProject.ProjectId) || !ProjectAccess.HasModule(objProject.Type)) return Json_AccessDenied();

                string type = objProject?.Type;
                var objProjecthistory = _context.Using<ProjectSiteDoc_History>().GetByCondition(x => x.ProjectId == objProject.ProjectId && x.Type == type && x.IsLatest == true).FirstOrDefault();
                if (objProject != null)
				{
					_context.Using<ProjectSiteDoc>().Delete(objProject);
					if(objProjecthistory != null)
					{
						_context.Using<ProjectSiteDoc_History>().Delete(objProjecthistory);

                    }
					

					CommonViewModel.IsConfirm = true;
					CommonViewModel.IsSuccess = true;
					CommonViewModel.StatusCode = ResponseStatusCode.Success;
					CommonViewModel.Message = "Data deleted successfully ! ";

					return Json(CommonViewModel);
				}

			}
			catch (Exception ex) { LogService.LogInsert(GetCurrentAction(), "", ex); }

			CommonViewModel.IsSuccess = false;
			CommonViewModel.StatusCode = ResponseStatusCode.Error;
			CommonViewModel.Message = ResponseStatusMessage.Unable_Delete;

			return Json(CommonViewModel);
		}

		public ActionResult Partial_AddEditForm_MP(long ProjectId = 0, string Type = null, long ProjectSiteMatId = 0)
		{
			if (!IsProjectAllowed(ProjectId) || !ProjectAccess.CanView(PermissionModules.MPRO)) return Json(null);

			var _CommonViewModel = new ResponseModel<ProjectSiteMaterial>() { Obj = new ProjectSiteMaterial() { ProjectId = ProjectId, UOM = "No" } };

			var objProject = (from z in _context.Using<Project>().GetByCondition(x => x.Id == ProjectId && x.VendorId == Logged_In_VendorId).ToList()
							  where z.IsActive == true
							  select new Project
							  {
								  //VendorId = z.VendorId,
								  Name = z.Name,
								  Description = z.Description,
								  StartDate = z.StartDate,
								  HandoverDate = z.HandoverDate,
								  Address = z.Address,
								  CityId = z.CityId,
								  StateId = z.StateId,
								  CountryId = z.CountryId,
								  LocationLink = z.LocationLink,
								  CoordinatorId = z.CoordinatorId,
								  CoordinatorName = z.CoordinatorName,
								  SiteDetails = z.SiteDetails,
								  StartDate_Text = z.StartDate.ToString("dd/MM/yyyy").Replace("-", "/"),
								  HandoverDate_Text = z.HandoverDate.HasValue ? z.HandoverDate.Value.ToString("dd/MM/yyyy").Replace("-", "/") : string.Empty,
								  IsActive = z.IsActive
							  }).FirstOrDefault();

			if (objProject == null || (string.IsNullOrEmpty(Type) && ProjectSiteMatId == 0)) return Json(null);

			if (ProjectSiteMatId > 0)
			{
				var obj = _context.Using<ProjectSiteMaterial>().GetByCondition(x => x.Id == ProjectSiteMatId && x.ProjectId == ProjectId && x.IsActive == true).FirstOrDefault();

				return Json(obj);
			}
			else
			{
				_CommonViewModel.SelectListItems = new List<SelectListItem_Custom>();

				var listUOM = _context.Using<UnitsOfMeasurement>().GetAll().ToList();

				if (listUOM != null && listUOM.Count() > 0) _CommonViewModel.SelectListItems.AddRange(listUOM.Select(x => new SelectListItem_Custom(x.Code, x.Name, x.Category)).ToList());

				_CommonViewModel.ObjList = _context.Using<ProjectSiteMaterial>().GetByCondition(x => x.ProjectId == ProjectId && x.IsActive == true).Distinct().ToList();

				if (_CommonViewModel.ObjList != null || _CommonViewModel.ObjList.Count() > 0)
				{
					foreach (var item in _CommonViewModel.ObjList)
					{
						item.Status_Text = item.Status == "S" ? "Submitted" : (item.Status == "O" ? "Ordered" : (item.Status == "RC" ? "Received" : (item.Status == "R" ? "Rejected" : "")));
						if (item.StatusDate != null) item.StatusDate_Text = item.StatusDate.ToString(Common.DateTimeFormat_ddMMyyyy);
						if (!string.IsNullOrEmpty(item.UOM)) item.UOM_Text = listUOM != null ? listUOM.Where(x => x.Code == item.UOM).Select(x => x.Name).FirstOrDefault() : "";
					}
				}

				_CommonViewModel.Data5 = _context.Using<Employee>().GetByCondition(x => x.IsActive == true && x.Id == Logged_In_EmployeeId
									&& x.VendorId == Logged_In_VendorId).Select(x => x.UserType).FirstOrDefault();

				return PartialView("_Partial_AddEditForm_MP", _CommonViewModel);
			}
		}

		[HttpPost]
		public ActionResult Save_MP(ProjectSiteMaterial viewModel)
		{
			try
			{
				if (viewModel != null)
				{
					#region Validation

					if (viewModel.ProjectId <= 0)
					{
						CommonViewModel.IsSuccess = false;
						CommonViewModel.StatusCode = ResponseStatusCode.Error;
						CommonViewModel.Message = "Please select Project.";

						return Json(CommonViewModel);
					}

					if (string.IsNullOrEmpty(viewModel.MaterialFor))
					{
						CommonViewModel.IsSuccess = false;
						CommonViewModel.StatusCode = ResponseStatusCode.Error;
						CommonViewModel.Message = "Please enter Material For.";

						return Json(CommonViewModel);
					}

					if (string.IsNullOrEmpty(viewModel.MaterialName))
					{
						CommonViewModel.IsSuccess = false;
						CommonViewModel.StatusCode = ResponseStatusCode.Error;
						CommonViewModel.Message = "Please enter Material Name.";

						return Json(CommonViewModel);
					}

					if (viewModel.Id == 0 && viewModel.Qty <= 0)
					{
						CommonViewModel.IsSuccess = false;
						CommonViewModel.StatusCode = ResponseStatusCode.Error;
						CommonViewModel.Message = "Please enter Quantity.";

						return Json(CommonViewModel);
					}

					// Material Procurement : Vendor / Customer / Employee (except Architect, Purchaser) → Add, Co-ordinator → Receive, Manager → Update (Order / Reject)
					bool canEditMaterial = ProjectAccess.Check(ProjectAccess.CanUpdateMaterial, PermissionModules.MPRO, PermissionActions.EDIT);
					bool canReceiveMaterial = ProjectAccess.Check(ProjectAccess.CanReceiveMaterial, PermissionModules.MPRO, PermissionActions.RECEIVE);

					if (!IsProjectAllowed(viewModel.ProjectId) || !ProjectAccess.CanView(PermissionModules.MPRO)
						|| !(viewModel.Id > 0 ? (canEditMaterial || canReceiveMaterial) : ProjectAccess.Check(ProjectAccess.CanAddMaterial, PermissionModules.MPRO, PermissionActions.ADD))) return Json_AccessDenied();

					if (canEditMaterial && !(viewModel.Status == "O" || viewModel.Status == "R"))
					{
						CommonViewModel.IsSuccess = false;
						CommonViewModel.StatusCode = ResponseStatusCode.Error;
						CommonViewModel.Message = "Please select Status.";

						return Json(CommonViewModel);
					}

					#endregion

					#region Database-Transaction

					using (var transaction = _context.BeginTransaction())
					{
						try
						{
							if (IsCustomer) viewModel.CustomerId = Logged_In_CustomerId;
							else
							{
								viewModel.CustomerId = (from x in _context.Using<CustomerProjectMapping>().GetByCondition(x => x.VendorId == Logged_In_VendorId).Distinct().ToList()
														join z in _context.Using<Project>().GetByCondition(x => x.VendorId == Logged_In_VendorId).ToList() on x.ProjectId equals z.Id
														where z.IsActive == true && z.Id == viewModel.ProjectId && x.ProjectId == viewModel.ProjectId
														select x.CustomerId).FirstOrDefault();

							}

							ProjectSiteMaterial obj = _context.Using<ProjectSiteMaterial>().GetByCondition(x => x.Id == viewModel.Id && x.ProjectId == viewModel.ProjectId).FirstOrDefault();

							if (viewModel.Id > 0 && obj == null) { transaction.Rollback(); return Json_AccessDenied(); }

							if (obj != null && obj.Status != "R")
							{
								obj.MaterialFor = viewModel.MaterialFor;
								obj.MaterialName = viewModel.MaterialName;

								obj.MaterialCode = viewModel.MaterialCode;
								obj.MaterialBrand = viewModel.MaterialBrand;

								if (obj.Status == "S") obj.Qty = viewModel.Qty;

								if (canEditMaterial && obj.Status != viewModel.Status)
								{
									// Status O = Order, R = Reject
									if (!ProjectAccess.Check(ProjectAccess.CanUpdateMaterial, PermissionModules.MPRO, viewModel.Status == "O" ? PermissionActions.ORDER : PermissionActions.REJECT))
									{ transaction.Rollback(); return Json_AccessDenied(); }

									obj.Status = viewModel.Status; obj.StatusDate = DateTime.Now;
								}

								if (canEditMaterial) obj.Qty_Order = viewModel.Qty_Order;

								// Mark Received only when Receive Quantity is entered
								if (canReceiveMaterial && (viewModel.Qty_Receive ?? 0) > 0)
								{ obj.Qty_Receive = viewModel.Qty_Receive; obj.Status = "RC"; obj.StatusDate = DateTime.Now; }

								obj.UOM = viewModel.UOM;

								_context.Using<ProjectSiteMaterial>().Update(obj);
							}
							else if (obj != null && obj.Status == "R")
							{
								CommonViewModel.IsSuccess = false;
								CommonViewModel.StatusCode = ResponseStatusCode.Error;
								CommonViewModel.Message = "Record was rejected. Can not allow to update. ";

								return Json(CommonViewModel);
							}
							else
							{
								viewModel.Status = "S";
								viewModel.StatusDate = DateTime.Now;

								var _obj = _context.Using<ProjectSiteMaterial>().Add(viewModel);
								viewModel.Id = _obj.Id;
							}

							CommonViewModel.IsConfirm = true;
							CommonViewModel.IsSuccess = true;
							CommonViewModel.StatusCode = ResponseStatusCode.Success;
							CommonViewModel.Message = "Record saved successfully ! ";

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
		public ActionResult DeleteConfirmed_MP(long Id)
		{
			try
			{
				if (ProjectAccess.CanUpdateMaterial)
				{
					var objProject = _context.Using<ProjectSiteMaterial>().GetByCondition(x => x.Id == Id).FirstOrDefault();

					if (objProject != null && IsProjectAllowed(objProject.ProjectId))
					{
						_context.Using<ProjectSiteMaterial>().Delete(objProject);

						CommonViewModel.IsConfirm = true;
						CommonViewModel.IsSuccess = true;
						CommonViewModel.StatusCode = ResponseStatusCode.Success;
						CommonViewModel.Message = "Data deleted successfully ! ";

						return Json(CommonViewModel);
					}
				}

			}
			catch (Exception ex) { LogService.LogInsert(GetCurrentAction(), "", ex); }

			CommonViewModel.IsSuccess = false;
			CommonViewModel.StatusCode = ResponseStatusCode.Error;
			CommonViewModel.Message = ResponseStatusMessage.Unable_Delete;

			return Json(CommonViewModel);
		}

		public ActionResult Partial_AddEditForm_WP(long CustomerId = 0, long ProjectId = 0, string Type = null, long ProjectSitePendingWorkId = 0)
		{
			CustomerId = (IsCustomer ? Logged_In_CustomerId : CustomerId);

			var _CommonViewModel = new ResponseModel<ProjectSitePendingWork>() { Obj = new ProjectSitePendingWork() { ProjectId = ProjectId, CustomerId = CustomerId, UploadDate = DateTime.Now.Date } };

			if (!IsProjectAllowed(ProjectId) || !ProjectAccess.CanView(PermissionModules.WPEND)) return Json(null);

			var objProject = (from x in _context.Using<CustomerProjectMapping>().GetByCondition(x => (IsCustomer ? x.CustomerId == CustomerId : true) && x.ProjectId == ProjectId && (x.VendorId == Logged_In_VendorId)).Distinct().ToList()
							  join z in _context.Using<Project>().GetByCondition(x => x.VendorId == Logged_In_VendorId).ToList() on x.ProjectId equals z.Id
							  where z.IsActive == true
							  select new Project
							  {
								  //VendorId = z.VendorId,
								  Name = z.Name,
								  Description = z.Description,
								  StartDate = z.StartDate,
								  HandoverDate = z.HandoverDate,
								  Address = z.Address,
								  CityId = z.CityId,
								  StateId = z.StateId,
								  CountryId = z.CountryId,
								  LocationLink = z.LocationLink,
								  CoordinatorId = z.CoordinatorId,
								  CoordinatorName = z.CoordinatorName,
								  SiteDetails = z.SiteDetails,
								  StartDate_Text = z.StartDate.ToString("dd/MM/yyyy").Replace("-", "/"),
								  HandoverDate_Text = z.HandoverDate.HasValue ? z.HandoverDate.Value.ToString("dd/MM/yyyy").Replace("-", "/") : string.Empty,
								  IsActive = z.IsActive
							  }).FirstOrDefault();

			if (objProject == null || (string.IsNullOrEmpty(Type) && ProjectSitePendingWorkId == 0)) return Json(null);

			if (ProjectSitePendingWorkId > 0)
			{
				var obj = _context.Using<ProjectSitePendingWork>().GetByCondition(x => x.Id == ProjectSitePendingWorkId && x.ProjectId == ProjectId && x.IsActive == true).FirstOrDefault();

				if (obj != null) obj.UploadDate_Text = obj.UploadDate.ToString("yyyy-MM-dd");

				return Json(obj);
			}
			else
			{
				_CommonViewModel.ObjList = _context.Using<ProjectSitePendingWork>().GetByCondition(x => x.ProjectId == ProjectId && x.IsActive == true).Distinct().ToList();

				if (_CommonViewModel.ObjList != null || _CommonViewModel.ObjList.Count() > 0)
				{
					foreach (var item in _CommonViewModel.ObjList)
					{
						item.Status_Text = item.Status == "P" ? "Pending" : (item.Status == "C" ? "Completed" : (item.Status == "R" ? "Rejected" : ""));
						if (item.StatusDate != null) item.StatusDate_Text = item.StatusDate.ToString(Common.DateTimeFormat_ddMMyyyy);
					}
				}

				_CommonViewModel.SelectListItems = new List<SelectListItem_Custom>();

				_CommonViewModel.SelectListItems.Add(new SelectListItem_Custom("A", "Admin"));
				_CommonViewModel.SelectListItems.Add(new SelectListItem_Custom("C", "Customer"));

				return PartialView("_Partial_AddEditForm_WP", _CommonViewModel);
			}
		}

		[HttpPost]
		public ActionResult Save_WP(ProjectSitePendingWork viewModel)
		{
			try
			{
				if (viewModel != null)
				{
					#region Validation

					if (string.IsNullOrEmpty(viewModel.UploadDate_Text))
					{
						CommonViewModel.IsSuccess = false;
						CommonViewModel.StatusCode = ResponseStatusCode.Error;
						CommonViewModel.Message = "Please enter Upload Date.";

						return Json(CommonViewModel);
					}

					if (string.IsNullOrEmpty(viewModel.PendingFrom))
					{
						CommonViewModel.IsSuccess = false;
						CommonViewModel.StatusCode = ResponseStatusCode.Error;
						CommonViewModel.Message = "Please select Pending From.";

						return Json(CommonViewModel);
					}

					if (string.IsNullOrEmpty(viewModel.Remarks))
					{
						CommonViewModel.IsSuccess = false;
						CommonViewModel.StatusCode = ResponseStatusCode.Error;
						CommonViewModel.Message = "Please enter Remark.";

						return Json(CommonViewModel);
					}

					if (string.IsNullOrEmpty(viewModel.PendingPoint))
					{
						CommonViewModel.IsSuccess = false;
						CommonViewModel.StatusCode = ResponseStatusCode.Error;
						CommonViewModel.Message = "Please enter Pending Point.";

						return Json(CommonViewModel);
					}

					// Legacy : Add : Upload rights (Vendor side only, not Customer), Edit : Manager (control level)
					bool legacyCanSave = !IsCustomer && (viewModel.Id > 0 ? ProjectAccess.CanModify : ProjectAccess.CanUpload);

					if (!IsProjectAllowed(viewModel.ProjectId) || !ProjectAccess.CanView(PermissionModules.WPEND)
						|| !ProjectAccess.Check(legacyCanSave, PermissionModules.WPEND, viewModel.Id > 0 ? PermissionActions.EDIT : PermissionActions.ADD)) return Json_AccessDenied();

					if (IsCustomer) viewModel.CustomerId = Logged_In_CustomerId;

					#endregion

					#region Database-Transaction

					using (var transaction = _context.BeginTransaction())
					{
						try
						{
							if (!string.IsNullOrEmpty(viewModel.UploadDate_Text)) { try { viewModel.UploadDate = DateTime.ParseExact(viewModel.UploadDate_Text, "yyyy-MM-dd", CultureInfo.InvariantCulture); } catch { } }

							ProjectSitePendingWork obj = _context.Using<ProjectSitePendingWork>().GetByCondition(x => x.Id == viewModel.Id && x.ProjectId == viewModel.ProjectId).FirstOrDefault();

							if (viewModel.Id > 0 && obj == null) { transaction.Rollback(); return Json_AccessDenied(); }

							if (obj != null && obj.Status != "R")
							{
								obj.UploadDate = viewModel.UploadDate;
								obj.PendingFrom = viewModel.PendingFrom;
								obj.Remarks = viewModel.Remarks;
								obj.PendingPoint = viewModel.PendingPoint;

								_context.Using<ProjectSitePendingWork>().Update(obj);
							}
							else if (obj != null && obj.Status == "R")
							{
								CommonViewModel.IsSuccess = false;
								CommonViewModel.StatusCode = ResponseStatusCode.Error;
								CommonViewModel.Message = "Record was rejected. Can not allow to update. ";

								return Json(CommonViewModel);
							}
							else
							{
								viewModel.Status = "P";
								viewModel.StatusDate = DateTime.Now;

								var _obj = _context.Using<ProjectSitePendingWork>().Add(viewModel);
								viewModel.Id = _obj.Id;
							}

							CommonViewModel.IsConfirm = true;
							CommonViewModel.IsSuccess = true;
							CommonViewModel.StatusCode = ResponseStatusCode.Success;
							CommonViewModel.Message = "Record saved successfully ! ";

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
		public ActionResult DeleteConfirmed_WP(long Id)
		{
			try
			{
				var objProject = _context.Using<ProjectSitePendingWork>().GetByCondition(x => x.Id == Id).FirstOrDefault();

				if (objProject != null && ProjectAccess.CanModify && IsProjectAllowed(objProject.ProjectId))
				{
					_context.Using<ProjectSitePendingWork>().Delete(objProject);

					CommonViewModel.IsConfirm = true;
					CommonViewModel.IsSuccess = true;
					CommonViewModel.StatusCode = ResponseStatusCode.Success;
					CommonViewModel.Message = "Data deleted successfully ! ";

					return Json(CommonViewModel);
				}

			}
			catch (Exception ex) { LogService.LogInsert(GetCurrentAction(), "", ex); }

			CommonViewModel.IsSuccess = false;
			CommonViewModel.StatusCode = ResponseStatusCode.Error;
			CommonViewModel.Message = ResponseStatusMessage.Unable_Delete;

			return Json(CommonViewModel);
		}

		public ActionResult Partial_AddEditForm_WCR(long ProjectId = 0, string Type = null, long AgencyMasterId = 0)
		{
			var list = _context.Using<LOV>().GetByCondition(x => x.LOV_Column.ToUpper() == "WORKTYPE").ToList();

			var _CommonViewModel = new ResponseModel<AgencyMaster>() { Obj = new AgencyMaster() { ProjectId = ProjectId } };

			if (!IsProjectAllowed(ProjectId) || !ProjectAccess.CanView(PermissionModules.WCOMP)) return Json(null);

			var objProject = (from z in _context.Using<Project>().GetByCondition(x => x.Id == ProjectId && x.VendorId == Logged_In_VendorId).ToList()
							  where z.IsActive == true
							  select new Project
							  {
								  //VendorId = z.VendorId,
								  Name = z.Name,
								  Description = z.Description,
								  StartDate = z.StartDate,
								  HandoverDate = z.HandoverDate,
								  Address = z.Address,
								  CityId = z.CityId,
								  StateId = z.StateId,
								  CountryId = z.CountryId,
								  LocationLink = z.LocationLink,
								  CoordinatorId = z.CoordinatorId,
								  CoordinatorName = z.CoordinatorName,
								  SiteDetails = z.SiteDetails,
								  StartDate_Text = z.StartDate.ToString("dd/MM/yyyy").Replace("-", "/"),
								  HandoverDate_Text = z.HandoverDate.HasValue ? z.HandoverDate.Value.ToString("dd/MM/yyyy").Replace("-", "/") : string.Empty,
								  IsActive = z.IsActive
							  }).FirstOrDefault();

			if (objProject == null || (string.IsNullOrEmpty(Type) && AgencyMasterId == 0)) return Json(null);

			if (AgencyMasterId > 0)
			{
				var obj = _context.Using<AgencyMaster>().GetByCondition(x => x.Id == AgencyMasterId && x.ProjectId == ProjectId && x.IsActive == true).FirstOrDefault();

				if (obj != null) obj.Status = obj.Status == "P" && ProjectAccess.Check(IsCustomer, PermissionModules.WCOMP, PermissionActions.APPROVE) ? "A" : obj.Status;

				if (obj != null && !string.IsNullOrEmpty(obj.WorkType)) obj.WorkType_Text = list != null ? list.Where(x => x.LOV_Code == obj.WorkType).Select(x => x.LOV_Desc).FirstOrDefault() : "";

				return Json(obj);
			}
			else
			{
				_CommonViewModel.SelectListItems = new List<SelectListItem_Custom>();

				if (list != null && list.Count() > 0) _CommonViewModel.SelectListItems.AddRange(list.Select(x => new SelectListItem_Custom(x.LOV_Code, x.LOV_Desc, x.LOV_Column, x.DisplayOrder)).ToList());

				_CommonViewModel.ObjList = _context.Using<AgencyMaster>().GetByCondition(x => x.ProjectId == ProjectId && x.IsActive == true).Distinct().ToList();

				if (_CommonViewModel.ObjList != null || _CommonViewModel.ObjList.Count() > 0)
					foreach (var item in _CommonViewModel.ObjList)
					{
						item.Status_Text = item.Status == "P" ? "Pending" : (item.Status == "A" ? "Approved" : (item.Status == "R" ? "Rejected" : ""));
						if (!string.IsNullOrEmpty(item.WorkType)) item.WorkType_Text = list != null ? list.Where(x => x.LOV_Code == item.WorkType).Select(x => x.LOV_Desc).FirstOrDefault() : "";
					}

				_CommonViewModel.Data5 = _context.Using<Employee>().GetByCondition(x => x.IsActive == true && x.Id == Logged_In_EmployeeId
									&& x.VendorId == Logged_In_VendorId).Select(x => x.UserType).FirstOrDefault();

				return PartialView("_Partial_AddEditForm_WCR", _CommonViewModel);
			}
		}

		[HttpPost]
		public ActionResult Save_WCR(AgencyMaster viewModel)
		{
			try
			{
				// Work Completion Report : Manager add / edit, Customer approve
				bool canAddWCR = ProjectAccess.Check(ProjectAccess.Is(ProjectAccess.MANAGER), PermissionModules.WCOMP, PermissionActions.ADD);
				bool canEditWCR = ProjectAccess.Check(ProjectAccess.Is(ProjectAccess.MANAGER), PermissionModules.WCOMP, PermissionActions.EDIT);
				bool canApproveWCR = ProjectAccess.Check(IsCustomer, PermissionModules.WCOMP, PermissionActions.APPROVE);

				if (viewModel != null && (canAddWCR || canEditWCR || canApproveWCR))
				{
					#region Validation

					if (viewModel.ProjectId <= 0)
					{
						CommonViewModel.IsSuccess = false;
						CommonViewModel.StatusCode = ResponseStatusCode.Error;
						CommonViewModel.Message = "Please select Project.";

						return Json(CommonViewModel);
					}

					if (!IsProjectAllowed(viewModel.ProjectId) || !ProjectAccess.CanView(PermissionModules.WCOMP)) return Json_AccessDenied();

					// New record : Add. Existing record : Edit (Manager) or Approve (Customer)
					if (viewModel.Id <= 0 ? !canAddWCR : !(canEditWCR || canApproveWCR)) return Json_AccessDenied();

					if (string.IsNullOrEmpty(viewModel.Name))
					{
						CommonViewModel.IsSuccess = false;
						CommonViewModel.StatusCode = ResponseStatusCode.Error;
						CommonViewModel.Message = "Please enter Agency Name.";

						return Json(CommonViewModel);
					}

					if (string.IsNullOrEmpty(viewModel.WorkType))
					{
						CommonViewModel.IsSuccess = false;
						CommonViewModel.StatusCode = ResponseStatusCode.Error;
						CommonViewModel.Message = "Please select Type of Work.";

						return Json(CommonViewModel);
					}

					#endregion

					#region Database-Transaction

					using (var transaction = _context.BeginTransaction())
					{
						try
						{
							AgencyMaster obj = _context.Using<AgencyMaster>().GetByCondition(x => x.Id == viewModel.Id && x.ProjectId == viewModel.ProjectId).FirstOrDefault();

							if (viewModel.Id > 0 && obj == null) { transaction.Rollback(); return Json_AccessDenied(); }

							if (obj != null && obj.Status != "R")
							{
								if (canEditWCR)
								{
									obj.Name = viewModel.Name;
									obj.WorkType = viewModel.WorkType;
								}

								obj.Notes = viewModel.Notes;

								// Approve : Customer (WCOMP APPROVE)
								if (canApproveWCR && obj.Status == "P") obj.Status = "A";

								_context.Using<AgencyMaster>().Update(obj);
							}
							else if (obj != null && obj.Status == "R")
							{
								CommonViewModel.IsSuccess = false;
								CommonViewModel.StatusCode = ResponseStatusCode.Error;
								CommonViewModel.Message = "Record was rejected. Can not allow to update. ";

								return Json(CommonViewModel);
							}
							else
							{
								viewModel.Status = "P";

								var _obj = _context.Using<AgencyMaster>().Add(viewModel);
								viewModel.Id = _obj.Id;
							}

							CommonViewModel.IsConfirm = true;
							CommonViewModel.IsSuccess = true;
							CommonViewModel.StatusCode = ResponseStatusCode.Success;
							CommonViewModel.Message = "Record saved successfully ! ";

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
		public ActionResult DeleteConfirmed_WCR(long Id)
		{
			try
			{
				if (ProjectAccess.Is(ProjectAccess.MANAGER))
				{
					var objProject = _context.Using<AgencyMaster>().GetByCondition(x => x.Id == Id).FirstOrDefault();

					if (objProject != null && objProject.Status == "P" && IsProjectAllowed(objProject.ProjectId))
					{
						_context.Using<AgencyMaster>().Delete(objProject);

						CommonViewModel.IsConfirm = true;
						CommonViewModel.IsSuccess = true;
						CommonViewModel.StatusCode = ResponseStatusCode.Success;
						CommonViewModel.Message = "Data deleted successfully ! ";

						return Json(CommonViewModel);
					}
				}

			}
			catch (Exception ex) { LogService.LogInsert(GetCurrentAction(), "", ex); }

			CommonViewModel.IsSuccess = false;
			CommonViewModel.StatusCode = ResponseStatusCode.Error;
			CommonViewModel.Message = ResponseStatusMessage.Unable_Delete;

			return Json(CommonViewModel);
		}

		[HttpPost]
		public ActionResult Save_Doc_Status(long Id = 0, string Status = "", string Type = "")
		{
			try
			{
				if (!string.IsNullOrEmpty(Status) && Id > 0 && Logged_In_VendorId > 0)
				{
					#region Validation

					if (string.IsNullOrEmpty(Status))
					{
						CommonViewModel.IsSuccess = false;
						CommonViewModel.StatusCode = ResponseStatusCode.Error;
						CommonViewModel.Message = "Please enter Status.";

						return Json(CommonViewModel);
					}

					#endregion

					#region Database-Transaction

					using (var transaction = _context.BeginTransaction())
					{
						try
						{
							if (Type == "DOC")
							{
								ProjectSiteDoc obj = _context.Using<ProjectSiteDoc>().GetByCondition(x => x.Id == Id).FirstOrDefault();

								if (obj == null || !(Status == "A" || Status == "R")
									|| !ProjectAccess.Check(ProjectAccess.CanApproveDoc, obj.Type, Status == "A" ? PermissionActions.APPROVE : PermissionActions.REJECT)
									|| !IsProjectAllowed(obj.ProjectId) || !ProjectAccess.CanView(obj.Type)) { transaction.Rollback(); return Json_AccessDenied(); }

								obj.Status = Status;
								obj.StatusDate = DateTime.Now;

								_context.Using<ProjectSiteDoc>().Update(obj);

								var latestHistory = _context.Using<ProjectSiteDoc_History>().GetByCondition(x => x.ProjectId == obj.ProjectId && x.Type == obj.Type && x.IsLatest == true).FirstOrDefault();
								if (latestHistory != null)
								{
									latestHistory.Status = Status;
									latestHistory.StatusDate = DateTime.Now;
									_context.Using<ProjectSiteDoc_History>().Update(latestHistory);
								}
							}
							else if (Type == "WP")
							{
								ProjectSitePendingWork obj = _context.Using<ProjectSitePendingWork>().GetByCondition(x => x.Id == Id).FirstOrDefault();

								if (obj == null || !(Status == "C" || Status == "R")
									|| !ProjectAccess.Check(ProjectAccess.CanModify, PermissionModules.WPEND, Status == "C" ? PermissionActions.COMPLETE : PermissionActions.REJECT)
									|| !IsProjectAllowed(obj.ProjectId) || !ProjectAccess.CanView(PermissionModules.WPEND)) { transaction.Rollback(); return Json_AccessDenied(); }

								obj.Status = Status;
								obj.StatusDate = DateTime.Now;

								_context.Using<ProjectSitePendingWork>().Update(obj);
							}
							else if (Type == "MP")
							{
								ProjectSiteMaterial obj = _context.Using<ProjectSiteMaterial>().GetByCondition(x => x.Id == Id).FirstOrDefault();

								if (obj == null || !(Status == "O" || Status == "R")
									|| !ProjectAccess.Check(ProjectAccess.CanUpdateMaterial, PermissionModules.MPRO, Status == "O" ? PermissionActions.ORDER : PermissionActions.REJECT)
									|| !IsProjectAllowed(obj.ProjectId) || !ProjectAccess.CanView(PermissionModules.MPRO)) { transaction.Rollback(); return Json_AccessDenied(); }

								obj.Status = Status;
								obj.StatusDate = DateTime.Now;

								_context.Using<ProjectSiteMaterial>().Update(obj);
							}
							else if (Type == "WC")
							{
								AgencyMaster obj = _context.Using<AgencyMaster>().GetByCondition(x => x.Id == Id).FirstOrDefault();

								// Legacy : no role check. Database : WCOMP APPROVE / REJECT (Customer by default) - intentional tightening
								if (obj == null || !(Status == "A" || Status == "R")
									|| !ProjectAccess.Check(true, PermissionModules.WCOMP, Status == "A" ? PermissionActions.APPROVE : PermissionActions.REJECT)
									|| !IsProjectAllowed(obj.ProjectId) || !ProjectAccess.CanView(PermissionModules.WCOMP)) { transaction.Rollback(); return Json_AccessDenied(); }

								obj.Status = Status;

								_context.Using<AgencyMaster>().Update(obj);
							}
							else { transaction.Rollback(); return Json_AccessDenied(); }

							CommonViewModel.IsConfirm = true;
							CommonViewModel.IsSuccess = true;
							CommonViewModel.StatusCode = ResponseStatusCode.Success;
							CommonViewModel.Message = "Record saved successfully ! ";

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
		public ActionResult GetProjectByCustomerId(long CustomerId = 0)
		{
			CustomerId = (IsCustomer ? Logged_In_CustomerId : CustomerId);

			var listProject = (from x in _context.Using<Project>().GetByCondition(x => x.VendorId == Logged_In_VendorId).ToList()
							   join z in _context.Using<CustomerProjectMapping>().GetByCondition(x => x.VendorId == Logged_In_VendorId).Distinct().ToList() on x.Id equals z.ProjectId
							   into cpmGroup
							   from cpm in cpmGroup.DefaultIfEmpty()
							   where x.IsActive == true && (CustomerId > 0 ? (cpm == null ? 0 : cpm.CustomerId) == CustomerId : true)
							   select new { Value = x.Id, Text = x.Name }).ToList();

			if (IsEmployee)
			{
				var listProjectId = _context.Using<EmployeeProjectMapping>().GetByCondition(x => x.EmployeeId == Logged_In_EmployeeId).Select(x => x.ProjectId).Distinct().ToList();

				listProject = listProject.Where(p => listProjectId.Contains(p.Value)).ToList();
			}

			return Json(listProject);
		}

		public ActionResult ProjectDetail()
		{
			CommonViewModel.SelectListItems = new List<SelectListItem_Custom>();

			List<Customer> listCustomer = _context.Using<Customer>().GetByCondition(x => (IsCustomer ? x.Id == Logged_In_CustomerId : true)
			&& x.IsActive == true && x.VendorId == Logged_In_VendorId).ToList();

			var listProject = (from x in _context.Using<Project>().GetByCondition(x => x.VendorId == Logged_In_VendorId).ToList()
							   join z in _context.Using<CustomerProjectMapping>().GetByCondition(x => x.VendorId == Logged_In_VendorId).Distinct().ToList() on x.Id equals z.ProjectId
							   into cpmGroup
							   from cpm in cpmGroup.DefaultIfEmpty()
							   where x.IsActive == true
							   select new { ProjectId = x.Id, ProjectName = x.Name, CustomerId = (cpm == null ? (long?)null : cpm.CustomerId) }).ToList();

			if (IsCustomer)
			{
				var listProjectId = _context.Using<CustomerProjectMapping>().GetByCondition(x => x.CustomerId == Logged_In_CustomerId && x.VendorId == Logged_In_VendorId)
					.Select(x => x.ProjectId).Distinct().ToList();

				listProject = listProject.Where(p => listProjectId.Contains(p.ProjectId)).ToList();
			}

			if (IsEmployee)
			{
				var listProjectId = _context.Using<EmployeeProjectMapping>().GetByCondition(x => x.EmployeeId == Logged_In_EmployeeId && x.VendorId == Logged_In_VendorId)
					.Select(x => x.ProjectId).Distinct().ToList();

				listProject = listProject.Where(p => listProjectId.Contains(p.ProjectId)).ToList();

				var listCustomerId = _context.Using<CustomerProjectMapping>().GetByCondition(x => listProjectId.Contains(x.ProjectId) && x.VendorId == Logged_In_VendorId)
					.Select(x => x.CustomerId).Distinct().ToList();

				listCustomer = listCustomer.Where(x => listCustomerId.Contains(x.Id)).ToList();
			}

			if (listProject != null && listProject.Count > 0)
				CommonViewModel.SelectListItems.AddRange(listProject.Distinct().Select(x => new SelectListItem_Custom(x.ProjectId.ToString(), x.ProjectName, x.CustomerId.ToString(), "P")).ToList());

			if (listCustomer != null && listCustomer.Count > 0)
				CommonViewModel.SelectListItems.AddRange(listCustomer.Select(x => new SelectListItem_Custom(x.Id.ToString(), x.Fullname, "C")).ToList());

			return View(CommonViewModel);
		}

		[HttpPost]
		public ActionResult GetProjectDetail(long CustomerId = 0, long ProjectId = 0)
		{
			CustomerId = (IsCustomer ? Logged_In_CustomerId : CustomerId);

			var listProjectId = _context.Using<EmployeeProjectMapping>().GetByCondition(x => x.EmployeeId == Logged_In_EmployeeId).Select(x => x.ProjectId).Distinct().ToList();

			var objProject = (from x in _context.Using<Project>().GetByCondition(x => x.VendorId == Logged_In_VendorId).ToList()
							  join z in _context.Using<CustomerProjectMapping>().GetByCondition(x => x.VendorId == Logged_In_VendorId).Distinct().ToList() on x.Id equals z.ProjectId
							  into cpmGroup
							  from cpm in cpmGroup.DefaultIfEmpty()
								  //from x in _context.Using<CustomerProjectMapping>().GetByCondition(x => (CustomerId > 0 ? x.CustomerId == CustomerId : true) && x.ProjectId == ProjectId && (x.VendorId == Logged_In_VendorId)).Distinct().ToList()
								  //join z in _context.Using<Project>().GetByCondition(x => x.VendorId == Logged_In_VendorId).ToList() on x.ProjectId equals z.Id
							  where x.IsActive == true && (CustomerId > 0 ? (cpm == null ? 0 : cpm.CustomerId) == CustomerId : true)
							  && x.Id == ProjectId && (IsEmployee ? listProjectId.Contains(x.Id) : true)
							  select new Project
							  {
								  //VendorId = x.VendorId,
								  Name = x.Name,
								  Description = x.Description,
								  StartDate = x.StartDate,
								  HandoverDate = x.HandoverDate,
								  Address = x.Address,
								  CityId = x.CityId,
								  StateId = x.StateId,
								  CountryId = x.CountryId,
								  LocationLink = x.LocationLink,
								  CoordinatorId = x.CoordinatorId,
								  CoordinatorName = x.CoordinatorName,
								  SiteDetails = x.SiteDetails,
								  StartDate_Text = x.StartDate != DateTime.MinValue ? x.StartDate.ToString("dd/MM/yyyy").Replace("-", "/") : "",
								  HandoverDate_Text = x.HandoverDate != DateTime.MinValue && x.HandoverDate.HasValue ? x.HandoverDate.Value.ToString("dd/MM/yyyy").Replace("-", "/") : "",
								  IsActive = x.IsActive
							  }).FirstOrDefault();

			var listEmployee = _context.Using<Employee>().GetByCondition(x => x.VendorId == Logged_In_VendorId).Distinct().ToList();

			if (objProject != null)
			{
				// A Project can have multiple Co-ordinators (Project form selection + Employee assignments), shown comma separated.
				var listCoordinatorId = new List<long>();

				if (objProject.CoordinatorId > 0) listCoordinatorId.Add(objProject.CoordinatorId.Value);

				listCoordinatorId.AddRange((from m in _context.Using<EmployeeProjectMapping>().GetByCondition(x => x.ProjectId == objProject.Id && x.VendorId == Logged_In_VendorId).ToList()
											join e in listEmployee on m.EmployeeId equals e.Id
											where e.UserType == "COORD"
											select e.Id).ToList());

				objProject.CoordinatorName = string.Join(", ", listCoordinatorId.Distinct()
					.Select(x => listEmployee.Where(y => y.Id == x).Select(y => y.Fullname).FirstOrDefault())
					.Where(x => !string.IsNullOrEmpty(x)));
			}

			return Json(objProject);
		}

		public ActionResult DailyUpdate()
		{
			// Daily Update screen (DAILY) : legacy had no server check (menu only)
			if (!ProjectAccess.Check(true, PermissionModules.DAILY, PermissionActions.VIEW)) return RedirectToAction("Index", "Home", new { area = "Admin" });

			CommonViewModel.SelectListItems = new List<SelectListItem_Custom>();

			List<Customer> listCustomer = _context.Using<Customer>().GetByCondition(x => (IsCustomer ? x.Id == Logged_In_CustomerId : true)
			&& x.IsActive == true && x.VendorId == Logged_In_VendorId).ToList();

			if (listCustomer != null && listCustomer.Count > 0)
				CommonViewModel.SelectListItems.AddRange(listCustomer.Select(x => new SelectListItem_Custom(x.Id.ToString(), x.Fullname, "C")).ToList());

			var listProject = (from x in _context.Using<Project>().GetByCondition(x => x.VendorId == Logged_In_VendorId).ToList()
							   join z in _context.Using<CustomerProjectMapping>().GetByCondition(x => x.VendorId == Logged_In_VendorId).Distinct().ToList() on x.Id equals z.ProjectId
							   into cpmGroup
							   from cpm in cpmGroup.DefaultIfEmpty()
							   where x.IsActive == true
							   select new { ProjectId = x.Id, ProjectName = x.Name, CustomerId = (cpm == null ? (long?)null : cpm.CustomerId) }).ToList();

			//var listProject = (from x in _context.Using<CustomerProjectMapping>().GetByCondition(x => x.VendorId == Logged_In_VendorId).Distinct().ToList()
			//				   join y in listCustomer on x.CustomerId equals y.Id
			//				   join z in _context.Using<Project>().GetByCondition(x => x.VendorId == Logged_In_VendorId).ToList() on x.ProjectId equals z.Id
			//				   where z.IsActive == true
			//				   select new { ProjectId = z.Id, ProjectName = z.Name, CustomerId = x.CustomerId }).ToList();

			if (IsCustomer)
			{
				var listProjectId = _context.Using<CustomerProjectMapping>().GetByCondition(x => x.CustomerId == Logged_In_CustomerId && x.VendorId == Logged_In_VendorId)
					.Select(x => x.ProjectId).Distinct().ToList();

				listProject = listProject.Where(p => listProjectId.Contains(p.ProjectId)).ToList();
			}

			if (IsEmployee)
			{
				var listProjectId = _context.Using<EmployeeProjectMapping>().GetByCondition(x => x.EmployeeId == Logged_In_EmployeeId).Select(x => x.ProjectId).Distinct().ToList();

				listProject = listProject.Where(p => listProjectId.Contains(p.ProjectId)).ToList();
			}

			if (listProject != null && listProject.Count > 0)
				CommonViewModel.SelectListItems.AddRange(listProject.Distinct().Select(x => new SelectListItem_Custom(x.ProjectId.ToString(), x.ProjectName, x.CustomerId.ToString(), "P")).ToList());

			CommonViewModel.Data5 = _context.Using<Employee>().GetByCondition(x => x.IsActive == true && x.Id == Logged_In_EmployeeId
														&& x.VendorId == Logged_In_VendorId).Select(x => x.UserType).FirstOrDefault();

			return View(CommonViewModel);
		}

		public ActionResult GetData(JqueryDatatableParam param)
		{
			string ProjectId = HttpContext.Request.Query["ProjectId"];
			string CustomerId = HttpContext.Request.Query["CustomerId"];

			string Search_Term = HttpContext.Request.Query["sSearch"];
			string SortCol = HttpContext.Request.Query["iSortCol_0"];
			string SortDir = HttpContext.Request.Query["sSortDir_0"];
			string DisplayStart = HttpContext.Request.Query["iDisplayStart"];
			string DisplayLength = HttpContext.Request.Query["iDisplayLength"];

			DataTable dt = new DataTable();

			List<ProjectDailyUpdate> result = new List<ProjectDailyUpdate>();

			if (!ProjectAccess.Check(true, PermissionModules.DAILY, PermissionActions.VIEW))
				return Json(new { param.sEcho, iTotalRecords = 0, iTotalDisplayRecords = 0, aaData = result });

			try
			{
				if (IsCustomer) CustomerId = Logged_In_CustomerId.ToString();

				var VendorId = (IsVendor ? Logged_In_VendorId : 0);

				var parameters = new List<SqlParameter>();

				parameters.Add(new SqlParameter("Id", SqlDbType.BigInt) { Value = 0, IsNullable = true });
				parameters.Add(new SqlParameter("VendorId", SqlDbType.BigInt) { Value = VendorId, IsNullable = true });
				parameters.Add(new SqlParameter("CustomerId", SqlDbType.BigInt) { Value = !string.IsNullOrEmpty(CustomerId) ? Convert.ToInt64(CustomerId) : 0, IsNullable = true });
				parameters.Add(new SqlParameter("ProjectId", SqlDbType.BigInt) { Value = !string.IsNullOrEmpty(ProjectId) ? Convert.ToInt64(ProjectId) : 0, IsNullable = true });
				parameters.Add(new SqlParameter("Search_Term", SqlDbType.NVarChar) { Value = Search_Term, IsNullable = true });
				parameters.Add(new SqlParameter("SortCol", SqlDbType.Int) { Value = !string.IsNullOrEmpty(SortCol) ? Convert.ToInt32(SortCol) : 0, IsNullable = true });
				parameters.Add(new SqlParameter("SortDir", SqlDbType.NVarChar) { Value = SortDir, IsNullable = true });
				parameters.Add(new SqlParameter("DisplayStart", SqlDbType.Int) { Value = !string.IsNullOrEmpty(DisplayStart) ? Convert.ToInt32(DisplayStart) : 0, IsNullable = true });
				parameters.Add(new SqlParameter("DisplayLength", SqlDbType.Int) { Value = !string.IsNullOrEmpty(DisplayLength) ? Convert.ToInt32(DisplayLength) : 10, IsNullable = true });

				dt = DataContext_Command.ExecuteStoredProcedure_DataTable("SP_Project_Daily_Update_GET", parameters.ToList());

				if (dt != null && dt.Rows.Count > 0)
					foreach (DataRow dr in dt.Rows)
						result.Add(new ProjectDailyUpdate()
						{
							SrNo = dr["SrNo"] != DBNull.Value ? Convert.ToInt32(dr["SrNo"]) : 0,
							Id = dr["Id"] != DBNull.Value ? Convert.ToInt64(dr["Id"]) : 0,
							ProjectId = dr["ProjectId"] != DBNull.Value ? Convert.ToInt64(dr["ProjectId"]) : 0,
							Project_Name = dr["Project_Name"] != DBNull.Value ? Convert.ToString(dr["Project_Name"]) : "",
							//CustomerId = dr["CustomerId"] != DBNull.Value ? Convert.ToInt64(dr["CustomerId"]) : 0,
							//Customer_Name = dr["Customer_Name"] != DBNull.Value ? Convert.ToString(dr["Customer_Name"]) : "",
							Notes = dr["Notes"] != DBNull.Value ? Convert.ToString(dr["Notes"]) : "",
							Date_Text = dr["Date_Text"] != DBNull.Value ? Convert.ToString(dr["Date_Text"]) : "",
							FilePath = dr["FilePath"] != DBNull.Value ? Convert.ToString(dr["FilePath"]) : "",
							HasFiles = dr["HasFiles"] != DBNull.Value ? Convert.ToBoolean(dr["HasFiles"]) : false,
							CreatedBy = dr["CreatedBy"] != DBNull.Value ? Convert.ToInt32(dr["CreatedBy"]) : 0,
							LastModifiedBy = dr["LastModifiedBy"] != DBNull.Value ? Convert.ToInt32(dr["LastModifiedBy"]) : 0,
						});


				if (IsEmployee)
				{
					var listProjectId = _context.Using<EmployeeProjectMapping>().GetByCondition(x => x.EmployeeId == Logged_In_EmployeeId).Select(x => x.ProjectId).Distinct().ToList();

					// Employee can see only assigned projects : selected project must be assigned, else nothing
					if (!string.IsNullOrEmpty(ProjectId) && Convert.ToInt64(ProjectId) > 0 && !listProjectId.Contains(Convert.ToInt64(ProjectId)))
						return Json(new { param.sEcho, iTotalRecords = 0, iTotalDisplayRecords = 0, aaData = new List<ProjectDailyUpdate>() });

					result = result.Where(p => listProjectId.Contains(p.ProjectId)).ToList();
				}

			}
			catch { }

			return Json(new
			{
				param.sEcho,
				iTotalRecords = result.Count(),
				iTotalDisplayRecords = dt != null && dt.Rows.Count > 0 ? Convert.ToInt64(dt.Rows[0]["Total_Records"]?.ToString()) : 0,
				aaData = result
			});

		}


		public ActionResult Partial_AddEditForm_DailyUpdate(long Id = 0, long ProjectId = 0, bool IsShowImages = false)
		{
			var _CommonViewModel = new ResponseModel<ProjectDailyUpdate>() { Obj = new ProjectDailyUpdate() { ProjectId = ProjectId, Date = DateTime.Now.Date } };

			if (!IsProjectAllowed(ProjectId) || !ProjectAccess.Check(true, PermissionModules.DAILY, PermissionActions.VIEW)) return Json(null);

			// Form : new record needs ADD, existing record EDIT (else shown read-only). Legacy : no check (Add / Edit buttons hidden in DailyUpdate view)
			bool canSave = !IsShowImages && ProjectAccess.Check(true, PermissionModules.DAILY, Id > 0 ? PermissionActions.EDIT : PermissionActions.ADD);

			if (!IsShowImages && Id <= 0 && !canSave) return Json(null);

			if (Id > 0)
			{
				try
				{
					var parameters = new List<SqlParameter>();

					parameters.Add(new SqlParameter("Id", SqlDbType.BigInt) { Value = Id, IsNullable = true });
					parameters.Add(new SqlParameter("ProjectId", SqlDbType.BigInt) { Value = ProjectId, IsNullable = true });
					parameters.Add(new SqlParameter("Search_Term", SqlDbType.NVarChar) { Value = "", IsNullable = true });
					parameters.Add(new SqlParameter("SortCol", SqlDbType.Int) { Value = 1, IsNullable = true });
					parameters.Add(new SqlParameter("SortDir", SqlDbType.NVarChar) { Value = "asc", IsNullable = true });
					parameters.Add(new SqlParameter("DisplayStart", SqlDbType.Int) { Value = 0, IsNullable = true });
					parameters.Add(new SqlParameter("DisplayLength", SqlDbType.Int) { Value = 10, IsNullable = true });

					var ds = DataContext_Command.ExecuteStoredProcedure_DataSet("SP_Project_Daily_Update_GET", parameters.ToList());

					if (ds != null && ds.Tables.Count > 0 && ds.Tables[0] != null && ds.Tables[0].Rows.Count > 0)
						_CommonViewModel.Obj = new ProjectDailyUpdate()
						{
							Id = ds.Tables[0].Rows[0]["Id"] != DBNull.Value ? Convert.ToInt64(ds.Tables[0].Rows[0]["Id"]) : 0,
							ProjectId = ds.Tables[0].Rows[0]["ProjectId"] != DBNull.Value ? Convert.ToInt64(ds.Tables[0].Rows[0]["ProjectId"]) : 0,
							Project_Name = ds.Tables[0].Rows[0]["Project_Name"] != DBNull.Value ? Convert.ToString(ds.Tables[0].Rows[0]["Project_Name"]) : "",
							//CustomerId = ds.Tables[0].Rows[0]["CustomerId"] != DBNull.Value ? Convert.ToInt64(ds.Tables[0].Rows[0]["CustomerId"]) : 0,
							//Customer_Name = ds.Tables[0].Rows[0]["Customer_Name"] != DBNull.Value ? Convert.ToString(ds.Tables[0].Rows[0]["Customer_Name"]) : "",
							Notes = ds.Tables[0].Rows[0]["Notes"] != DBNull.Value ? Convert.ToString(ds.Tables[0].Rows[0]["Notes"]) : "",
							Date = ds.Tables[0].Rows[0]["Date"] != DBNull.Value ? Convert.ToDateTime(ds.Tables[0].Rows[0]["Date_Text"]) : nullDateTime,
							Date_Text = ds.Tables[0].Rows[0]["Date_Text"] != DBNull.Value ? Convert.ToString(ds.Tables[0].Rows[0]["Date_Text"]) : "",
							FilePath = ds.Tables[0].Rows[0]["FilePath"] != DBNull.Value ? Convert.ToString(ds.Tables[0].Rows[0]["FilePath"]) : "",
							HasFiles = ds.Tables[0].Rows[0]["HasFiles"] != DBNull.Value ? Convert.ToBoolean(ds.Tables[0].Rows[0]["HasFiles"]) : false,
							CreatedBy = ds.Tables[0].Rows[0]["CreatedBy"] != DBNull.Value ? Convert.ToInt32(ds.Tables[0].Rows[0]["CreatedBy"]) : 0,
							LastModifiedBy = ds.Tables[0].Rows[0]["LastModifiedBy"] != DBNull.Value ? Convert.ToInt32(ds.Tables[0].Rows[0]["LastModifiedBy"]) : 0,
							Files = new List<ProjectDailyUpdateFiles>()
						};

					if (ds != null && ds.Tables.Count > 1 && ds.Tables[1] != null && ds.Tables[1].Rows.Count > 0)
					{
						foreach (DataRow dr in ds.Tables[1].Rows)
							_CommonViewModel.Obj.Files.Add(new ProjectDailyUpdateFiles()
							{
								Id = dr["Id"] != DBNull.Value ? Convert.ToInt64(dr["Id"]) : 0,
								DailyUpdateId = dr["DailyUpdateId"] != DBNull.Value ? Convert.ToInt64(dr["DailyUpdateId"]) : 0,
								FileName = dr["FileName"] != DBNull.Value ? Convert.ToString(dr["FileName"]) : "",
								FileContentType = dr["FileContentType"] != DBNull.Value ? Convert.ToString(dr["FileContentType"]) : "",
								FileData = dr["FileData"] != DBNull.Value ? (byte[])dr["FileData"] : null
							});
					}
				}
				catch { }
			}

			// Scope OWN : only the user's own entries (created / last modified) are editable
			if (canSave && Id > 0 && ProjectAccess.Scope(PermissionModules.DAILY, PermissionActions.EDIT) == PermissionScopes.OWN
				&& !(_CommonViewModel.Obj.CreatedBy == Logged_In_UserId || _CommonViewModel.Obj.LastModifiedBy == Logged_In_UserId)) canSave = false;

			_CommonViewModel.Data4 = canSave;
			_CommonViewModel.Data5 = IsShowImages;

			return PartialView("Partial_AddEditForm_DailyUpdate", _CommonViewModel);
		}


		[HttpPost]
		public ActionResult Save_DailyUpdate(ProjectDailyUpdate viewModel)
		{
			try
			{
				// Active Employee of the logged-in vendor (identity check, kept in code)
				var objEmployee = viewModel != null && IsEmployee && Logged_In_EmployeeId > 0
					? _context.Using<Employee>().GetByCondition(x => x.IsActive == true && x.Id == Logged_In_EmployeeId && x.VendorId == Logged_In_VendorId).FirstOrDefault()
					: null;

				// Legacy : Co-ordinator only (Employee.UserType = "COORD"). Database : SITE ADD (new) / EDIT (existing)
				if (objEmployee != null && ProjectAccess.Check(string.Equals((objEmployee.UserType ?? "").TrimEnd(), "COORD", StringComparison.OrdinalIgnoreCase),
					PermissionModules.DAILY, viewModel.Id > 0 ? PermissionActions.EDIT : PermissionActions.ADD))
				{
					#region Validation

					if (viewModel.ProjectId <= 0)
					{
						CommonViewModel.IsSuccess = false;
						CommonViewModel.StatusCode = ResponseStatusCode.Error;
						CommonViewModel.Message = "Please select Project.";

						return Json(CommonViewModel);
					}

					if (!IsProjectAllowed(viewModel.ProjectId)) return Json_AccessDenied();

					// Existing entry must belong to the posted (allowed) project : no cross-project overwrite. Scope OWN : own entries only
					if (viewModel.Id > 0)
					{
						var objExisting = _context.Using<ProjectDailyUpdate>().GetByCondition(x => x.Id == viewModel.Id).FirstOrDefault();

						if (objExisting == null || objExisting.ProjectId != viewModel.ProjectId) return Json_AccessDenied();

						if (ProjectAccess.Scope(PermissionModules.DAILY, PermissionActions.EDIT) == PermissionScopes.OWN
							&& !(objExisting.CreatedBy == Logged_In_UserId || objExisting.LastModifiedBy == Logged_In_UserId)) return Json_AccessDenied();
					}

					if (string.IsNullOrEmpty(viewModel.Date_Text))
					{
						CommonViewModel.IsSuccess = false;
						CommonViewModel.StatusCode = ResponseStatusCode.Error;
						CommonViewModel.Message = "Please select Date.";

						return Json(CommonViewModel);
					}

					if (string.IsNullOrEmpty(viewModel.Notes))
					{
						CommonViewModel.IsSuccess = false;
						CommonViewModel.StatusCode = ResponseStatusCode.Error;
						CommonViewModel.Message = "Please enter Note(s).";

						return Json(CommonViewModel);
					}

					if (!string.IsNullOrEmpty(viewModel.Date_Text)) { try { viewModel.Date = DateTime.ParseExact(viewModel.Date_Text, "yyyy-MM-dd", CultureInfo.InvariantCulture); } catch { } }

					if (viewModel.Id == 0 && null != _context.Using<ProjectDailyUpdate>().GetByCondition(x => x.Date.HasValue && x.Date.Value.Date == viewModel.Date.Value.Date && x.ProjectId == viewModel.ProjectId).FirstOrDefault())
					{
						CommonViewModel.IsSuccess = false;
						CommonViewModel.StatusCode = ResponseStatusCode.Error;
						CommonViewModel.Message = "Already Note(s) or Image(s) added on this selected date.";

						return Json(CommonViewModel);
					}

					var files = AppHttpContextAccessor.AppHttpContext.Request.Form.Files;

					//if (string.IsNullOrEmpty(viewModel.Notes) && (files == null || files.Count() <= 0))
					//{
					//	CommonViewModel.IsSuccess = false;
					//	CommonViewModel.StatusCode = ResponseStatusCode.Error;
					//	CommonViewModel.Message = "Please enter Note(s) or Upload Image(s).";

					//	return Json(CommonViewModel);
					//}

					#endregion

					#region Database-Transaction

					using (var transaction = _context.BeginTransaction())
					{
						try
						{
							ProjectDailyUpdate obj = _context.Using<ProjectDailyUpdate>().GetByCondition(x => x.Id == viewModel.Id && x.ProjectId == viewModel.ProjectId).FirstOrDefault();

							if (obj != null)
							{
								obj.Notes = viewModel.Notes;

								_context.Using<ProjectDailyUpdate>().Update(obj);
							}
							else
							{
								viewModel.CustomerId = 0;
								var _obj = _context.Using<ProjectDailyUpdate>().Add(viewModel);
								viewModel.Id = _obj.Id;
							}

							CommonViewModel.IsConfirm = true;
							CommonViewModel.IsSuccess = true;
							CommonViewModel.StatusCode = ResponseStatusCode.Success;
							CommonViewModel.Message = "Record saved successfully ! ";

							CommonViewModel.RedirectURL = Url.Action("DailyUpdate", "Project", new { area = "Admin" });

							try
							{
								if (files != null && files.Count() > 0)
								{
									foreach (var file in files)
									{
										using (var memoryStream = new MemoryStream())
										{
											file.CopyTo(memoryStream);

											if (memoryStream.ToArray().Length > 0)
											{
												var objFile = new ProjectDailyUpdateFiles();

												objFile.DailyUpdateId = viewModel.Id;
												objFile.FileName = Path.GetFileName(file.FileName);
												objFile.FileContentType = file.ContentType;
												objFile.FileData = memoryStream.ToArray();

												_context.Using<ProjectDailyUpdateFiles>().Add(objFile);
											}
										}
									}
								}
							}
							catch (Exception ex)
							{
								LogService.LogInsert(GetCurrentAction(), "", ex);
								//CommonViewModel.Message = "Issue in Uploading Image/PDF.";
								//CommonViewModel.IsSuccess = false;
								//CommonViewModel.StatusCode = ResponseStatusCode.Error;
							}

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

		public IActionResult ViewDocument(long ProjectId = 0, long ProjectSiteDocId = 0, long CustomerId = 0)
		{
			if (!IsProjectAllowed(ProjectId)) return NotFound();

			CustomerId = (IsCustomer ? Logged_In_CustomerId : CustomerId);

			var obj = _context.Using<ProjectSiteDoc>().GetByCondition(x => x.Id == ProjectSiteDocId && x.ProjectId == ProjectId && x.IsActive == true).FirstOrDefault();
			if (obj?.FileData == null || !ProjectAccess.CanView(obj.Type)) return NotFound();

			Set_ContentDisposition_Inline(obj.FileName);

			return File(obj.FileData, obj.FileContentType ?? "application/pdf");
		}

		public IActionResult ViewDocument_DailyUpdateFile(long DailyUpdateFileId)
		{
			if (!ProjectAccess.Check(true, PermissionModules.DAILY, PermissionActions.VIEW)) return NotFound();

			var obj = _context.Using<ProjectDailyUpdateFiles>().GetByCondition(x => x.Id == DailyUpdateFileId).FirstOrDefault();
			if (obj?.FileData == null) return NotFound();

			var objDailyUpdate = _context.Using<ProjectDailyUpdate>().GetByCondition(x => x.Id == obj.DailyUpdateId).FirstOrDefault();
			if (objDailyUpdate == null || !IsProjectAllowed(objDailyUpdate.ProjectId)) return NotFound();

			Set_ContentDisposition_Inline(obj.FileName);

			return File(obj.FileData, obj.FileContentType ?? "application/pdf");
		}

		[HttpPost]
		public IActionResult Delete_DailyUpdateFile(long DailyUpdateFileId)
		{
			try
			{
				var file = _context.Using<ProjectDailyUpdateFiles>().GetByCondition(x => x.Id == DailyUpdateFileId).FirstOrDefault();

				var objDailyUpdate = file != null ? _context.Using<ProjectDailyUpdate>().GetByCondition(x => x.Id == file.DailyUpdateId).FirstOrDefault() : null;

				// Only Manager or the employee who created / modified the daily update can delete its file (same as DailyUpdate view)
				if (objDailyUpdate == null || !IsProjectAllowed(objDailyUpdate.ProjectId)
					|| !(ProjectAccess.Is(ProjectAccess.MANAGER) || objDailyUpdate.CreatedBy == Logged_In_UserId || objDailyUpdate.LastModifiedBy == Logged_In_UserId))
					return Json_AccessDenied();

				if (file != null)
				{
					_context.Using<ProjectDailyUpdateFiles>().Delete(file);

					CommonViewModel.IsConfirm = true;
					CommonViewModel.IsSuccess = true;
					CommonViewModel.StatusCode = ResponseStatusCode.Success;
					CommonViewModel.Message = "File deleted successfully ! ";

					return Json(CommonViewModel);
				}
			}
			catch (Exception ex) { LogService.LogInsert(GetCurrentAction(), "", ex); }

			CommonViewModel.IsSuccess = false;
			CommonViewModel.StatusCode = ResponseStatusCode.Error;
			CommonViewModel.Message = ResponseStatusMessage.Unable_Delete;

			return Json(CommonViewModel);
		}

		#region Project Access

		/// <summary> Project belongs to logged-in Vendor and logged-in Customer / Employee is mapped (assigned) to it. </summary>
		private bool IsProjectAllowed(long ProjectId)
		{
			if (ProjectId <= 0 || Logged_In_VendorId <= 0) return false;

			if (!_context.Using<Project>().Any(x => x.Id == ProjectId && x.VendorId == Logged_In_VendorId)) return false;

			if (IsCustomer) return _context.Using<CustomerProjectMapping>().Any(x => x.ProjectId == ProjectId && x.CustomerId == Logged_In_CustomerId && x.VendorId == Logged_In_VendorId);

			if (IsEmployee) return _context.Using<EmployeeProjectMapping>().Any(x => x.ProjectId == ProjectId && x.EmployeeId == Logged_In_EmployeeId);

			return IsVendor;
		}

		private JsonResult Json_AccessDenied()
		{
			CommonViewModel.IsSuccess = false;
			CommonViewModel.StatusCode = ResponseStatusCode.Error;
			CommonViewModel.Message = "You do not have access to perform this action.";

			return Json(CommonViewModel);
		}

		private void Set_ContentDisposition_Inline(string fileName)
		{
			var contentDisposition = new Microsoft.Net.Http.Headers.ContentDispositionHeaderValue("inline");
			contentDisposition.SetHttpFileName(string.IsNullOrEmpty(fileName) ? "document" : fileName);

			Response.Headers[Microsoft.Net.Http.Headers.HeaderNames.ContentDisposition] = contentDisposition.ToString();
		}

		#endregion
	}

}