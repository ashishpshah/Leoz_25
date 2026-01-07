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

			if (listEmployee != null && listEmployee.Count > 0 && CommonViewModel.ObjList != null && CommonViewModel.ObjList.Count() > 0)
				foreach (var item in CommonViewModel.ObjList)
					item.CoordinatorName = listEmployee.Where(x => x.Id == item.CoordinatorId).Select(x => x.Fullname).FirstOrDefault();

			return View(CommonViewModel);
		}

		//[CustomAuthorizeAttribute(AccessType_Enum.Read)]
		public ActionResult Partial_AddEditForm(long Id = 0)
		{
			CommonViewModel.Obj = new Project() { StartDate = DateTime.Now };

			if (Id > 0) CommonViewModel.Obj = _context.Using<Project>().GetByCondition(x => x.Id == Id && IsVendor == true ? x.VendorId == Logged_In_VendorId : false).FirstOrDefault();

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
				var subscription = _context.Using<VendorSubscription>().GetByCondition(x => x.VendorId == Logged_In_VendorId).OrderByDescending(x => x.EndDate.Ticks).FirstOrDefault();

				if (subscription == null)
				{
					CommonViewModel.IsSuccess = false;
					CommonViewModel.StatusCode = ResponseStatusCode.Error;
					CommonViewModel.Message = "You are not subscribe any plan.";

					return Json(CommonViewModel);
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

					if (viewModel.Id == 0 && string.IsNullOrEmpty(viewModel.StartDate_Text))
					{
						CommonViewModel.IsSuccess = false;
						CommonViewModel.StatusCode = ResponseStatusCode.Error;
						CommonViewModel.Message = "Please enter Password.";

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
				var objProject = _context.Using<Project>().GetByCondition(x => x.Id == Id && x.VendorId == Logged_In_VendorId).FirstOrDefault();

				if (objProject != null)
				{
					_context.Using<Project>().Delete(objProject);

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

				var obj = _context.Using<ProjectSiteDoc>().GetByCondition(x => x.Id == ProjectSiteDocId && x.ProjectId == ProjectId && (IsCustomer ? (x.CustomerId == CustomerId || x.CustomerId == 0) : true) && x.IsActive == true).FirstOrDefault();
				if (obj != null) obj.UploadDate_Text = obj.UploadDate.ToString("yyyy-MM-dd");

				return Json(obj);
			}
			else
			{
				_CommonViewModel.ObjList = _context.Using<ProjectSiteDoc>().GetByCondition(x => x.ProjectId == ProjectId && (IsCustomer ? (x.CustomerId == CustomerId || x.CustomerId == 0) : true) && x.IsActive == true && x.Type == Type).Distinct().ToList();

				if (_CommonViewModel.ObjList != null || _CommonViewModel.ObjList.Count() > 0)
				{
					foreach (var item in _CommonViewModel.ObjList)
					{
						item.Status_Text = item.Status == "U" ? "Upload" : (item.Status == "A" ? "Approved" : (item.Status == "R" ? "Rejected" : ""));
						if (item.StatusDate != null) item.StatusDate_Text = item.StatusDate.ToString(Common.DateTimeFormat_ddMMyyyy);
					}
				}

				return PartialView("_Partial_AddEditForm_Doc", _CommonViewModel);
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

					if (string.IsNullOrEmpty(viewModel.UploadDate_Text))
					{
						CommonViewModel.IsSuccess = false;
						CommonViewModel.StatusCode = ResponseStatusCode.Error;
						CommonViewModel.Message = "Please enter Upload Date.";

						return Json(CommonViewModel);
					}

					var files = AppHttpContextAccessor.AppHttpContext.Request.Form.Files;

					if (viewModel.Id == 0 && (files == null || files.Count() <= 0))
					{
						CommonViewModel.IsSuccess = false;
						CommonViewModel.StatusCode = ResponseStatusCode.Error;
						CommonViewModel.Message = "Please upload file.";

						return Json(CommonViewModel);
					}

					if (string.IsNullOrEmpty(viewModel.Remark))
					{
						CommonViewModel.IsSuccess = false;
						CommonViewModel.StatusCode = ResponseStatusCode.Error;
						CommonViewModel.Message = "Please enter Remarks.";

						return Json(CommonViewModel);
					}

					#endregion

					#region Database-Transaction

					using (var transaction = _context.BeginTransaction())
					{
						try
						{
							if (!string.IsNullOrEmpty(viewModel.UploadDate_Text)) { try { viewModel.UploadDate = DateTime.ParseExact(viewModel.UploadDate_Text, "yyyy-MM-dd", CultureInfo.InvariantCulture); } catch { } }

							ProjectSiteDoc obj = _context.Using<ProjectSiteDoc>().GetByCondition(x => x.Id == viewModel.Id).FirstOrDefault();

							if (obj != null && obj.Status != "R")
							{
								obj.Remark = viewModel.Remark;
								obj.UploadDate = viewModel.UploadDate;
								//obj.FilePath = !string.IsNullOrEmpty(viewModel.FilePath) ? viewModel.FilePath : obj.FilePath;
								obj.Type = viewModel.Type;

								_context.Using<ProjectSiteDoc>().Update(obj);
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
								viewModel.Id = _obj.Id;
							}

							CommonViewModel.IsConfirm = true;
							CommonViewModel.IsSuccess = true;
							CommonViewModel.StatusCode = ResponseStatusCode.Success;
							CommonViewModel.Message = "Record saved successfully ! ";

							try
							{
								if (files != null && files.Count() > 0 && files[0].Length > 0)
								{
									string folderPath = Path.Combine(AppHttpContextAccessor.WebRootPath, "Uploads", "ProjectSiteDoc", $"{viewModel.ProjectId}");

									if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

									var file = files[0];
									string fileName = $"{viewModel.Id} " + Path.GetFileName(file.FileName); // Ensure file name is safe
									string filePath = Path.Combine(folderPath, fileName);

									// Save the file
									using (var stream = new FileStream(filePath, FileMode.Create))
									{
										file.CopyTo(stream);
									}

									obj = _context.Using<ProjectSiteDoc>().GetByCondition(x => x.Id == viewModel.Id).FirstOrDefault();

									if (obj != null)
									{
										obj.FilePath = filePath.Replace(AppHttpContextAccessor.WebRootPath, "").Replace("\\", "/");

										_context.Using<ProjectSiteDoc>().Update(obj);
									}
								}
							}
							catch (Exception)
							{
								CommonViewModel.Message = "Issue in Uploading Image/PDF.";
								CommonViewModel.IsSuccess = false;
								CommonViewModel.StatusCode = ResponseStatusCode.Error;
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
		public ActionResult DeleteConfirmed_Doc(long Id)
		{
			try
			{
				var objProject = _context.Using<ProjectSiteDoc>().GetByCondition(x => x.Id == Id).FirstOrDefault();

				if (objProject != null)
				{
					_context.Using<ProjectSiteDoc>().Delete(objProject);

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
			if (IsCustomer) return Json(null);

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
				if (viewModel != null && !IsCustomer)
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

					#endregion

					#region Database-Transaction

					using (var transaction = _context.BeginTransaction())
					{
						try
						{
							if (IsCustomer) return Json(null);

							if (!IsCustomer)
							{
								viewModel.CustomerId = (from x in _context.Using<CustomerProjectMapping>().GetByCondition(x => x.VendorId == Logged_In_VendorId).Distinct().ToList()
														join z in _context.Using<Project>().GetByCondition(x => x.VendorId == Logged_In_VendorId).ToList() on x.ProjectId equals z.Id
														where z.IsActive == true && z.Id == viewModel.ProjectId && x.ProjectId == viewModel.ProjectId
														select x.CustomerId).FirstOrDefault();

							}

							ProjectSiteMaterial obj = _context.Using<ProjectSiteMaterial>().GetByCondition(x => x.Id == viewModel.Id).FirstOrDefault();

							if (obj != null && obj.Status != "R")
							{
								obj.MaterialFor = viewModel.MaterialFor;
								obj.MaterialName = viewModel.MaterialName;

								obj.MaterialCode = viewModel.MaterialCode;
								obj.MaterialBrand = viewModel.MaterialBrand;

								if (obj.Status == "S") obj.Qty = viewModel.Qty;

								if (_context.Using<Employee>().Any(x => x.Id == Logged_In_EmployeeId && x.UserType == "MNGR" && x.IsActive == true && x.VendorId == Logged_In_VendorId))
								{ obj.Qty_Order = viewModel.Qty_Order; obj.Status = viewModel.Status; }

								if (_context.Using<Employee>().Any(x => x.Id == Logged_In_EmployeeId && x.UserType == "COORD" && x.IsActive == true && x.VendorId == Logged_In_VendorId))
								{ obj.Qty_Receive = viewModel.Qty_Receive; obj.Status = "RC"; }

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
				if (_context.Using<Employee>().Any(x => x.Id == Logged_In_EmployeeId && (x.UserType == "MNGR" || x.UserType == "COORD") && x.IsActive == true && x.VendorId == Logged_In_VendorId))
				{
					var objProject = _context.Using<ProjectSiteMaterial>().GetByCondition(x => x.Id == Id).FirstOrDefault();

					if (objProject != null)
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
				var obj = _context.Using<ProjectSitePendingWork>().GetByCondition(x => x.Id == ProjectSitePendingWorkId && x.ProjectId == ProjectId && (IsCustomer ? x.CustomerId == CustomerId : true) && x.IsActive == true).FirstOrDefault();

				if (obj != null) obj.UploadDate_Text = obj.UploadDate.ToString("yyyy-MM-dd");

				return Json(obj);
			}
			else
			{
				_CommonViewModel.ObjList = _context.Using<ProjectSitePendingWork>().GetByCondition(x => x.ProjectId == ProjectId && (IsCustomer ? (x.CustomerId == CustomerId || x.CustomerId == 0) : true) && x.IsActive == true).Distinct().ToList();

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

					#endregion

					#region Database-Transaction

					using (var transaction = _context.BeginTransaction())
					{
						try
						{
							if (!string.IsNullOrEmpty(viewModel.UploadDate_Text)) { try { viewModel.UploadDate = DateTime.ParseExact(viewModel.UploadDate_Text, "yyyy-MM-dd", CultureInfo.InvariantCulture); } catch { } }

							ProjectSitePendingWork obj = _context.Using<ProjectSitePendingWork>().GetByCondition(x => x.Id == viewModel.Id).FirstOrDefault();

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

				if (objProject != null)
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

				if (obj != null) obj.Status = obj.Status == "P" && IsCustomer ? "A" : obj.Status;

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
						item.Status_Text = item.Status == "P" ? "Pending" : (item.Status == "A" ? "Approve" : (item.Status == "R" ? "Rejected" : ""));
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
				if (viewModel != null && !_context.Using<Employee>().Any(x => x.Id == Logged_In_EmployeeId && x.UserType == "COORD" && x.VendorId == Logged_In_VendorId))
				{
					#region Validation

					if (viewModel.ProjectId <= 0)
					{
						CommonViewModel.IsSuccess = false;
						CommonViewModel.StatusCode = ResponseStatusCode.Error;
						CommonViewModel.Message = "Please select Project.";

						return Json(CommonViewModel);
					}

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
							AgencyMaster obj = _context.Using<AgencyMaster>().GetByCondition(x => x.Id == viewModel.Id).FirstOrDefault();

							if (obj != null && obj.Status != "R"
								&& (IsCustomer || _context.Using<Employee>().Any(x => x.Id == Logged_In_EmployeeId && x.UserType == "MNGR" && x.IsActive == true && x.VendorId == Logged_In_VendorId)))
							{
								if (!IsCustomer)
								{
									obj.Name = viewModel.Name;
									obj.WorkType = viewModel.WorkType;
								}

								obj.Notes = viewModel.Notes;

								obj.Status = viewModel.Status;

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
				if (IsCustomer || _context.Using<Employee>().Any(x => x.Id == Logged_In_EmployeeId && (x.UserType == "MNGR") && x.IsActive == true && x.VendorId == Logged_In_VendorId))
				{
					var objProject = _context.Using<AgencyMaster>().GetByCondition(x => x.Id == Id).FirstOrDefault();

					if (objProject != null)
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

								if (obj != null)
								{
									obj.Status = Status;
									obj.StatusDate = DateTime.Now;

									_context.Using<ProjectSiteDoc>().Update(obj);
								}
							}
							else if (Type == "WP")
							{
								ProjectSitePendingWork obj = _context.Using<ProjectSitePendingWork>().GetByCondition(x => x.Id == Id).FirstOrDefault();

								if (obj != null)
								{
									obj.Status = Status;
									obj.StatusDate = DateTime.Now;

									_context.Using<ProjectSitePendingWork>().Update(obj);
								}
							}
							else if (Type == "MP")
							{
								ProjectSiteMaterial obj = _context.Using<ProjectSiteMaterial>().GetByCondition(x => x.Id == Id).FirstOrDefault();

								if (obj != null)
								{
									obj.Status = Status;
									obj.StatusDate = DateTime.Now;

									_context.Using<ProjectSiteMaterial>().Update(obj);
								}
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
		public ActionResult GetProjectByCustomerId(long CustomerId = 0)
		{
			CustomerId = (IsCustomer ? Logged_In_CustomerId : CustomerId);

			var listProject = (from x in _context.Using<CustomerProjectMapping>().GetByCondition(x => x.CustomerId == CustomerId && (x.VendorId == Logged_In_VendorId)).Distinct().ToList()
							   join z in _context.Using<Project>().GetByCondition(x => x.VendorId == Logged_In_VendorId).ToList() on x.ProjectId equals z.Id
							   where z.IsActive == true
							   select new { Value = z.Id, Text = z.Name }).ToList();

			return Json(listProject);
		}

		public ActionResult ProjectDetail()
		{
			CommonViewModel.SelectListItems = new List<SelectListItem_Custom>();

			List<Customer> listCustomer = _context.Using<Customer>().GetByCondition(x => (IsCustomer ? x.Id == Logged_In_CustomerId : true) && x.IsActive == true && x.VendorId == Logged_In_VendorId).ToList();

			if (listCustomer != null && listCustomer.Count > 0 && IsVendor)
				CommonViewModel.SelectListItems.AddRange(listCustomer.Select(x => new SelectListItem_Custom(x.Id.ToString(), x.Fullname, "C")).ToList());

			var listProject = (from x in _context.Using<CustomerProjectMapping>().GetByCondition(x => x.VendorId == Logged_In_VendorId).Distinct().ToList()
							   join y in listCustomer on x.CustomerId equals y.Id
							   join z in _context.Using<Project>().GetByCondition(x => x.VendorId == Logged_In_VendorId).ToList() on x.ProjectId equals z.Id
							   where z.IsActive == true
							   select new { ProjectId = z.Id, ProjectName = z.Name, CustomerId = x.CustomerId }).ToList();

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
			}

			if (listProject != null && listProject.Count > 0)
				CommonViewModel.SelectListItems.AddRange(listProject.Distinct().Select(x => new SelectListItem_Custom(x.ProjectId.ToString(), x.ProjectName, x.CustomerId.ToString(), "P")).ToList());

			return View(CommonViewModel);
		}

		[HttpPost]
		public ActionResult GetProjectDetail(long CustomerId = 0, long ProjectId = 0)
		{
			CustomerId = (IsCustomer ? Logged_In_CustomerId : CustomerId);

			var objProject = (from x in _context.Using<CustomerProjectMapping>().GetByCondition(x => (CustomerId > 0 ? x.CustomerId == CustomerId : true) && x.ProjectId == ProjectId && (x.VendorId == Logged_In_VendorId)).Distinct().ToList()
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
								  StartDate_Text = z.StartDate != DateTime.MinValue ? z.StartDate.ToString("dd/MM/yyyy").Replace("-", "/") : "",
								  HandoverDate_Text = z.HandoverDate != DateTime.MinValue && z.HandoverDate.HasValue ? z.HandoverDate.Value.ToString("dd/MM/yyyy").Replace("-", "/") : "",
								  IsActive = z.IsActive
							  }).FirstOrDefault();

			var listEmployee = _context.Using<Employee>().GetByCondition(x => x.VendorId == Logged_In_VendorId).Distinct().ToList();

			if (listEmployee != null && listEmployee.Count > 0 && objProject != null)
				objProject.CoordinatorName = listEmployee.Where(x => x.Id == objProject.CoordinatorId).Select(x => x.Fullname).FirstOrDefault();

			return Json(objProject);
		}

		public ActionResult DailyUpdate()
		{
			CommonViewModel.SelectListItems = new List<SelectListItem_Custom>();

			List<Customer> listCustomer = _context.Using<Customer>().GetByCondition(x => (IsCustomer ? x.Id == Logged_In_CustomerId : true)
			&& x.IsActive == true && x.VendorId == Logged_In_VendorId).ToList();

			if (listCustomer != null && listCustomer.Count > 0)
				CommonViewModel.SelectListItems.AddRange(listCustomer.Select(x => new SelectListItem_Custom(x.Id.ToString(), x.Fullname, "C")).ToList());

			var listProject = (from x in _context.Using<CustomerProjectMapping>().GetByCondition(x => x.VendorId == Logged_In_VendorId).Distinct().ToList()
							   join y in listCustomer on x.CustomerId equals y.Id
							   join z in _context.Using<Project>().GetByCondition(x => x.VendorId == Logged_In_VendorId).ToList() on x.ProjectId equals z.Id
							   where z.IsActive == true
							   select new { ProjectId = z.Id, ProjectName = z.Name, CustomerId = x.CustomerId }).ToList();

			if (IsCustomer)
			{
				var listProjectId = _context.Using<CustomerProjectMapping>().GetByCondition(x => x.CustomerId == Logged_In_CustomerId && x.VendorId == Logged_In_VendorId)
					.Select(x => x.ProjectId).Distinct().ToList();

				listProject = listProject.Where(p => listProjectId.Contains(p.ProjectId)).ToList();
			}

			if (IsEmployee)
			{
				var listProjectId = _context.Using<EmployeeProjectMapping>().GetByCondition(x => x.EmployeeId == Logged_In_UserId).Select(x => x.ProjectId).Distinct().ToList();

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
							FilePath = dr["FilePath"] != DBNull.Value ? Convert.ToString(dr["FilePath"]) : ""
						});


				if (IsEmployee)
				{
					var listProjectId = _context.Using<EmployeeProjectMapping>().GetByCondition(x => x.EmployeeId == Logged_In_EmployeeId).Select(x => x.ProjectId).Distinct().ToList();

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

					var dt = DataContext_Command.ExecuteStoredProcedure_DataTable("SP_Project_Daily_Update_GET", parameters.ToList());

					if (dt != null && dt.Rows.Count > 0)
						_CommonViewModel.Obj = new ProjectDailyUpdate()
						{
							Id = dt.Rows[0]["Id"] != DBNull.Value ? Convert.ToInt64(dt.Rows[0]["Id"]) : 0,
							ProjectId = dt.Rows[0]["ProjectId"] != DBNull.Value ? Convert.ToInt64(dt.Rows[0]["ProjectId"]) : 0,
							Project_Name = dt.Rows[0]["Project_Name"] != DBNull.Value ? Convert.ToString(dt.Rows[0]["Project_Name"]) : "",
							//CustomerId = dt.Rows[0]["CustomerId"] != DBNull.Value ? Convert.ToInt64(dt.Rows[0]["CustomerId"]) : 0,
							//Customer_Name = dt.Rows[0]["Customer_Name"] != DBNull.Value ? Convert.ToString(dt.Rows[0]["Customer_Name"]) : "",
							Notes = dt.Rows[0]["Notes"] != DBNull.Value ? Convert.ToString(dt.Rows[0]["Notes"]) : "",
							Date = dt.Rows[0]["Date"] != DBNull.Value ? Convert.ToDateTime(dt.Rows[0]["Date_Text"]) : nullDateTime,
							Date_Text = dt.Rows[0]["Date_Text"] != DBNull.Value ? Convert.ToString(dt.Rows[0]["Date_Text"]) : "",
							FilePath = dt.Rows[0]["FilePath"] != DBNull.Value ? Convert.ToString(dt.Rows[0]["FilePath"]) : ""
						};
				}
				catch { }
			}

			_CommonViewModel.Data5 = IsShowImages;

			return PartialView("Partial_AddEditForm_DailyUpdate", _CommonViewModel);
		}


		[HttpPost]
		public ActionResult Save_DailyUpdate(ProjectDailyUpdate viewModel)
		{
			try
			{
				if (viewModel != null && IsEmployee && Logged_In_EmployeeId > 0
					&& _context.Using<Employee>().Any(x => x.IsActive == true && x.Id == Logged_In_EmployeeId && x.UserType == "COORD" && x.VendorId == Logged_In_VendorId))
				{
					#region Validation

					if (viewModel.ProjectId <= 0)
					{
						CommonViewModel.IsSuccess = false;
						CommonViewModel.StatusCode = ResponseStatusCode.Error;
						CommonViewModel.Message = "Please select Project.";

						return Json(CommonViewModel);
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
							ProjectDailyUpdate obj = _context.Using<ProjectDailyUpdate>().GetByCondition(x => x.Id == viewModel.Id).FirstOrDefault();

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
									List<string> filePaths = new List<string>();

									string folderPath = Path.Combine(AppHttpContextAccessor.WebRootPath, "Uploads", "Project_Daily_Update", $"{viewModel.ProjectId}");

									if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

									foreach (var file in files)
									{
										string fileName = $"{viewModel.Id} " + Path.GetFileName(file.FileName);
										string filePath = Path.Combine(folderPath, fileName);

										int counter = 1;
										while (System.IO.File.Exists(filePath))
										{
											// If the file exists, append a number to the file name
											string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(file.FileName);
											string fileExtension = Path.GetExtension(file.FileName);
											string newFileName = $"{fileNameWithoutExtension}_{counter}{fileExtension}";

											filePath = Path.Combine(folderPath, newFileName);
											counter++;
										}

										using (var stream = new FileStream(filePath, FileMode.Create)) { file.CopyTo(stream); }

										filePaths.Add(filePath.Replace(AppHttpContextAccessor.WebRootPath, "").Replace("\\", "/"));
									}

									if (filePaths != null && filePaths.Count() > 0)
									{
										obj = _context.Using<ProjectDailyUpdate>().GetByCondition(x => x.Id == viewModel.Id).FirstOrDefault();

										if (obj != null)
										{
											if (!string.IsNullOrEmpty(obj.FilePath)) filePaths.Insert(0, obj.FilePath);

											obj.FilePath = string.Join(",", filePaths.ToArray());

											_context.Using<ProjectDailyUpdate>().Update(obj);
										}
									}
								}
							}
							catch (Exception)
							{
								CommonViewModel.Message = "Issue in Uploading Image/PDF.";
								CommonViewModel.IsSuccess = false;
								CommonViewModel.StatusCode = ResponseStatusCode.Error;
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

	}

}