using Leoz_25.Controllers;
using Leoz_25.Infra;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Build.Evaluation;
using Microsoft.CodeAnalysis;
using System.Data;
using System.Globalization;
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
			CommonViewModel.ObjList = _context.Using<Project>().GetByCondition(x => Logged_In_VendorId > 0 ? x.VendorId == Logged_In_VendorId : false).ToList();

			var listEmployee = _context.Using<Employee>().GetByCondition(x => Logged_In_VendorId > 0 ? x.VendorId == Logged_In_VendorId : false).Distinct().ToList();

			if (listEmployee != null && listEmployee.Count > 0 && CommonViewModel.ObjList != null && CommonViewModel.ObjList.Count() > 0)
				foreach (var item in CommonViewModel.ObjList)
					item.CoordinatorName = listEmployee.Where(x => x.Id == item.CoordinatorId).Select(x => x.Fullname).FirstOrDefault();

			return View(CommonViewModel);
		}

		//[CustomAuthorizeAttribute(AccessType_Enum.Read)]
		public ActionResult Partial_AddEditForm(long Id = 0)
		{
			CommonViewModel.Obj = new Project() { StartDate = DateTime.Now };

			if (Id > 0) CommonViewModel.Obj = _context.Using<Project>().GetByCondition(x => x.Id == Id && Logged_In_VendorId > 0 ? x.VendorId == Logged_In_VendorId : false).FirstOrDefault();

			CommonViewModel.SelectListItems = new List<SelectListItem_Custom>();

			var listEmployee = _context.Using<Employee>().GetByCondition(x => (x.IsActive == true || x.Id == (CommonViewModel.Obj != null ? CommonViewModel.Obj.CoordinatorId : -1))
									&& Logged_In_VendorId > 0 ? x.VendorId == Logged_In_VendorId : false).Distinct().ToList();

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
							catch (Exception ex) { transaction.Rollback(); }
						}

					}
					catch (Exception ex) { }

					#endregion
				}
			}
			catch (Exception ex) { }

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
			catch (Exception ex) { }

			CommonViewModel.IsSuccess = false;
			CommonViewModel.StatusCode = ResponseStatusCode.Error;
			CommonViewModel.Message = ResponseStatusMessage.Unable_Delete;

			return Json(CommonViewModel);
		}

		public ActionResult ProjectDetail()
		{
			var _Logged_In_VendorId = (Logged_In_CustomerId <= 0) ? Logged_In_VendorId : Logged_In_Customer_VendorId;

			CommonViewModel.SelectListItems = new List<SelectListItem_Custom>();

			List<Customer> listCustomer = _context.Using<Customer>().GetByCondition(x => ((Logged_In_CustomerId <= 0 ? true : x.UserId == Logged_In_CustomerId)) && x.IsActive == true && (_Logged_In_VendorId > 0 ? x.VendorId == _Logged_In_VendorId : false)).ToList();

			if (listCustomer != null && listCustomer.Count > 0)
				CommonViewModel.SelectListItems.AddRange(listCustomer.Select(x => new SelectListItem_Custom(x.Id.ToString(), x.Fullname, "C")).ToList());

			var listProject = (from x in _context.Using<CustomerProjectMapping>().GetByCondition(x => (_Logged_In_VendorId > 0 ? x.VendorId == _Logged_In_VendorId : false)).Distinct().ToList()
							   join y in listCustomer on x.CustomerId equals y.Id
							   join z in _context.Using<Project>().GetByCondition(x => _Logged_In_VendorId > 0 ? x.VendorId == _Logged_In_VendorId : false).ToList() on x.ProjectId equals z.Id
							   where z.IsActive == true
							   select new { ProjectId = z.Id, ProjectName = z.Name }).ToList();

			if (listProject != null && listProject.Count > 0)
				CommonViewModel.SelectListItems.AddRange(listProject.Select(x => new SelectListItem_Custom(x.ProjectId.ToString(), x.ProjectName, "P")).ToList());

			return View(CommonViewModel);
		}



		public ActionResult Partial_AddEditForm_Doc(long CustomerId = 0, long ProjectId = 0, string Type = null, long ProjectSiteDocId = 0)
		{
			var _CommonViewModel = new ResponseModel<ProjectSiteDoc>() { Obj = new ProjectSiteDoc() { Type = Type, ProjectId = ProjectId, CustomerId = CustomerId, UploadDate = DateTime.Now.Date } };

			var _Logged_In_VendorId = (Logged_In_CustomerId <= 0) ? Logged_In_VendorId : Logged_In_Customer_VendorId;

			var objProject = (from x in _context.Using<CustomerProjectMapping>().GetByCondition(x => x.CustomerId == CustomerId && x.ProjectId == ProjectId && (_Logged_In_VendorId > 0 ? x.VendorId == _Logged_In_VendorId : false)).Distinct().ToList()
							  join z in _context.Using<Project>().GetByCondition(x => _Logged_In_VendorId > 0 ? x.VendorId == _Logged_In_VendorId : false).ToList() on x.ProjectId equals z.Id
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
				var obj = _context.Using<ProjectSiteDoc>().GetByCondition(x => x.Id == ProjectSiteDocId && x.ProjectId == ProjectId && x.CustomerId == CustomerId && x.IsActive == true && x.Type == Type).FirstOrDefault();

				if (obj != null) obj.UploadDate_Text = obj.UploadDate.ToString("yyyy-MM-dd");

				return Json(obj);
			}
			else
			{
				_CommonViewModel.ObjList = _context.Using<ProjectSiteDoc>().GetByCondition(x => x.ProjectId == ProjectId && x.CustomerId == CustomerId && x.IsActive == true && x.Type == Type).Distinct().ToList();

				if (_CommonViewModel.ObjList != null || _CommonViewModel.ObjList.Count() > 0)
				{
					foreach (var item in _CommonViewModel.ObjList)
					{
						item.Status_Text = item.Status == "U" ? "Upload" : (item.Status == "A" ? "Approved" : (item.Status == "R" ? "Rejected" : ""));
					}
				}

				_CommonViewModel.Data5 = Logged_In_Customer_VendorId;

				return PartialView("_Partial_AddEditForm_Doc", _CommonViewModel);
			}
		}

		[HttpPost]
		public ActionResult Save_Doc(ProjectSiteDoc viewModel)
		{
			try
			{
				if (viewModel != null && Logged_In_Customer_VendorId == 0)
				{
					#region Validation

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

							if (obj != null)
							{
								obj.Remark = viewModel.Remark;
								obj.UploadDate = viewModel.UploadDate;
								//obj.FilePath = !string.IsNullOrEmpty(viewModel.FilePath) ? viewModel.FilePath : obj.FilePath;
								obj.Type = viewModel.Type;

								_context.Using<ProjectSiteDoc>().Update(obj);
							}
							else
							{
								viewModel.Status = "U";
								viewModel.StatusDate = DateTime.Now;

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
						catch (Exception ex) { transaction.Rollback(); }
					}

					#endregion
				}
			}
			catch (Exception ex) { }

			CommonViewModel.Message = ResponseStatusMessage.Error;
			CommonViewModel.IsSuccess = false;
			CommonViewModel.StatusCode = ResponseStatusCode.Error;

			return Json(CommonViewModel);
		}

		[HttpPost]
		public ActionResult Save_Doc_Status(ProjectSiteDoc viewModel)
		{
			try
			{
				if (viewModel != null && Logged_In_Customer_VendorId > 0 && viewModel.Id > 0)
				{
					#region Validation

					if (string.IsNullOrEmpty(viewModel.Status))
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
							ProjectSiteDoc obj = _context.Using<ProjectSiteDoc>().GetByCondition(x => x.Id == viewModel.Id).FirstOrDefault();

							if (obj != null)
							{
								obj.Status = viewModel.Status;

								_context.Using<ProjectSiteDoc>().Update(obj);
							}

							CommonViewModel.IsConfirm = true;
							CommonViewModel.IsSuccess = true;
							CommonViewModel.StatusCode = ResponseStatusCode.Success;
							CommonViewModel.Message = "Record saved successfully ! ";

							transaction.Commit();

							return Json(CommonViewModel);
						}
						catch (Exception ex) { transaction.Rollback(); }
					}

					#endregion
				}
			}
			catch (Exception ex) { }

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

				if (objProject != null && Logged_In_Customer_VendorId == 0)
				{
					_context.Using<ProjectSiteDoc>().Delete(objProject);

					CommonViewModel.IsConfirm = true;
					CommonViewModel.IsSuccess = true;
					CommonViewModel.StatusCode = ResponseStatusCode.Success;
					CommonViewModel.Message = "Data deleted successfully ! ";

					return Json(CommonViewModel);
				}

			}
			catch (Exception ex) { }

			CommonViewModel.IsSuccess = false;
			CommonViewModel.StatusCode = ResponseStatusCode.Error;
			CommonViewModel.Message = ResponseStatusMessage.Unable_Delete;

			return Json(CommonViewModel);
		}


		public ActionResult Partial_AddEditForm_MP(long CustomerId = 0, long ProjectId = 0, string Type = null, long ProjectSiteMatId = 0)
		{
			var _CommonViewModel = new ResponseModel<ProjectSiteMaterial>() { Obj = new ProjectSiteMaterial() { ProjectId = ProjectId, CustomerId = CustomerId } };

			var _Logged_In_VendorId = (Logged_In_CustomerId <= 0) ? Logged_In_VendorId : Logged_In_Customer_VendorId;

			var objProject = (from x in _context.Using<CustomerProjectMapping>().GetByCondition(x => x.CustomerId == CustomerId && x.ProjectId == ProjectId && (_Logged_In_VendorId > 0 ? x.VendorId == _Logged_In_VendorId : false)).Distinct().ToList()
							  join z in _context.Using<Project>().GetByCondition(x => _Logged_In_VendorId > 0 ? x.VendorId == _Logged_In_VendorId : false).ToList() on x.ProjectId equals z.Id
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
				var obj = _context.Using<ProjectSiteMaterial>().GetByCondition(x => x.Id == ProjectSiteMatId && x.ProjectId == ProjectId && x.CustomerId == CustomerId && x.IsActive == true).FirstOrDefault();

				return Json(obj);
			}
			else
			{
				_CommonViewModel.ObjList = _context.Using<ProjectSiteMaterial>().GetByCondition(x => x.ProjectId == ProjectId && x.CustomerId == CustomerId && x.IsActive == true).Distinct().ToList();

				if (_CommonViewModel.ObjList != null || _CommonViewModel.ObjList.Count() > 0)
				{
					foreach (var item in _CommonViewModel.ObjList)
					{
						item.Status_Text = item.Status == "S" ? "Submitted" : (item.Status == "A" ? "Approved" : (item.Status == "R" ? "Rejected" : ""));
					}
				}

				_CommonViewModel.SelectListItems = new List<SelectListItem_Custom>();

				var listUOM = _context.Using<UnitsOfMeasurement>().GetAll().ToList();

				if (listUOM != null && listUOM.Count() > 0) _CommonViewModel.SelectListItems.AddRange(listUOM.Select(x => new SelectListItem_Custom(x.Code, x.Name, x.Category)).ToList());

				_CommonViewModel.Data5 = Logged_In_Customer_VendorId;

				return PartialView("_Partial_AddEditForm_MP", _CommonViewModel);
			}
		}

		[HttpPost]
		public ActionResult Save_MP(ProjectSiteMaterial viewModel)
		{
			try
			{
				if (viewModel != null && Logged_In_Customer_VendorId == 0)
				{
					#region Validation

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
							ProjectSiteMaterial obj = _context.Using<ProjectSiteMaterial>().GetByCondition(x => x.Id == viewModel.Id).FirstOrDefault();

							if (obj != null)
							{
								obj.MaterialFor = viewModel.MaterialFor;
								obj.MaterialName = viewModel.MaterialName;

								obj.Qty = viewModel.Qty;
								obj.UOM = viewModel.UOM;

								_context.Using<ProjectSiteMaterial>().Update(obj);
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
						catch (Exception ex) { transaction.Rollback(); }
					}

					#endregion
				}
			}
			catch (Exception ex) { }

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
				var objProject = _context.Using<ProjectSiteMaterial>().GetByCondition(x => x.Id == Id).FirstOrDefault();

				if (objProject != null && Logged_In_Customer_VendorId == 0)
				{
					_context.Using<ProjectSiteMaterial>().Delete(objProject);

					CommonViewModel.IsConfirm = true;
					CommonViewModel.IsSuccess = true;
					CommonViewModel.StatusCode = ResponseStatusCode.Success;
					CommonViewModel.Message = "Data deleted successfully ! ";

					return Json(CommonViewModel);
				}

			}
			catch (Exception ex) { }

			CommonViewModel.IsSuccess = false;
			CommonViewModel.StatusCode = ResponseStatusCode.Error;
			CommonViewModel.Message = ResponseStatusMessage.Unable_Delete;

			return Json(CommonViewModel);
		}



		public ActionResult Partial_AddEditForm_WP(long CustomerId = 0, long ProjectId = 0, string Type = null, long ProjectSitePendingWorkId = 0)
		{
			var _CommonViewModel = new ResponseModel<ProjectSitePendingWork>() { Obj = new ProjectSitePendingWork() { ProjectId = ProjectId, CustomerId = CustomerId, UploadDate = DateTime.Now.Date } };

			var _Logged_In_VendorId = (Logged_In_CustomerId <= 0) ? Logged_In_VendorId : Logged_In_Customer_VendorId;

			var objProject = (from x in _context.Using<CustomerProjectMapping>().GetByCondition(x => x.CustomerId == CustomerId && x.ProjectId == ProjectId && (_Logged_In_VendorId > 0 ? x.VendorId == _Logged_In_VendorId : false)).Distinct().ToList()
							  join z in _context.Using<Project>().GetByCondition(x => _Logged_In_VendorId > 0 ? x.VendorId == _Logged_In_VendorId : false).ToList() on x.ProjectId equals z.Id
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
				var obj = _context.Using<ProjectSitePendingWork>().GetByCondition(x => x.Id == ProjectSitePendingWorkId && x.ProjectId == ProjectId && x.CustomerId == CustomerId && x.IsActive == true).FirstOrDefault();

				if (obj != null) obj.UploadDate_Text = obj.UploadDate.ToString("yyyy-MM-dd");

				return Json(obj);
			}
			else
			{
				_CommonViewModel.ObjList = _context.Using<ProjectSitePendingWork>().GetByCondition(x => x.ProjectId == ProjectId && x.CustomerId == CustomerId && x.IsActive == true).Distinct().ToList();

				if (_CommonViewModel.ObjList != null || _CommonViewModel.ObjList.Count() > 0)
				{
					foreach (var item in _CommonViewModel.ObjList)
					{
						item.Status_Text = item.Status == "P" ? "Pending" : (item.Status == "C" ? "Confirm" : (item.Status == "R" ? "Rejected" : ""));
					}
				}

				_CommonViewModel.SelectListItems = new List<SelectListItem_Custom>();

				_CommonViewModel.SelectListItems.Add(new SelectListItem_Custom("A", "Admin"));
				_CommonViewModel.SelectListItems.Add(new SelectListItem_Custom("C", "Customer"));

				_CommonViewModel.Data5 = Logged_In_Customer_VendorId;

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

							if (obj != null)
							{
								obj.UploadDate = viewModel.UploadDate;
								obj.PendingFrom = viewModel.PendingFrom;
								obj.Remarks = viewModel.Remarks;
								obj.PendingPoint = viewModel.PendingPoint;

								_context.Using<ProjectSitePendingWork>().Update(obj);
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
						catch (Exception ex) { transaction.Rollback(); }
					}

					#endregion
				}
			}
			catch (Exception ex) { }

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
			catch (Exception ex) { }

			CommonViewModel.IsSuccess = false;
			CommonViewModel.StatusCode = ResponseStatusCode.Error;
			CommonViewModel.Message = ResponseStatusMessage.Unable_Delete;

			return Json(CommonViewModel);
		}


		[HttpPost]
		public ActionResult GetProjectByCustomerId(long CustomerId = 0)
		{
			var _Logged_In_VendorId = (Logged_In_CustomerId <= 0) ? Logged_In_VendorId : Logged_In_Customer_VendorId;

			var listProject = (from x in _context.Using<CustomerProjectMapping>().GetByCondition(x => x.CustomerId == CustomerId && (_Logged_In_VendorId > 0 ? x.VendorId == _Logged_In_VendorId : false)).Distinct().ToList()
							   join z in _context.Using<Project>().GetByCondition(x => _Logged_In_VendorId > 0 ? x.VendorId == _Logged_In_VendorId : false).ToList() on x.ProjectId equals z.Id
							   where z.IsActive == true
							   select new { Value = z.Id, Text = z.Name }).ToList();

			return Json(listProject);
		}

		[HttpPost]
		public ActionResult GetProjectDetail(long CustomerId = 0, long ProjectId = 0)
		{
			var _Logged_In_VendorId = (Logged_In_CustomerId <= 0) ? Logged_In_VendorId : Logged_In_Customer_VendorId;

			var objProject = (from x in _context.Using<CustomerProjectMapping>().GetByCondition(x => x.CustomerId == CustomerId && x.ProjectId == ProjectId && (_Logged_In_VendorId > 0 ? x.VendorId == _Logged_In_VendorId : false)).Distinct().ToList()
							  join z in _context.Using<Project>().GetByCondition(x => _Logged_In_VendorId > 0 ? x.VendorId == _Logged_In_VendorId : false).ToList() on x.ProjectId equals z.Id
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

			var listEmployee = _context.Using<Employee>().GetByCondition(x => Logged_In_VendorId > 0 ? x.VendorId == Logged_In_VendorId : false).Distinct().ToList();

			if (listEmployee != null && listEmployee.Count > 0 && objProject != null)
				objProject.CoordinatorName = listEmployee.Where(x => x.Id == objProject.CoordinatorId).Select(x => x.Fullname).FirstOrDefault();

			return Json(objProject);
		}

	}

}