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

			for (int i = 0; i < CommonViewModel.ObjList?.Count(); i++)
				CommonViewModel.ObjList[i].UserType = listUOM.FirstOrDefault(x => x.LOV_Code == CommonViewModel.ObjList[i].UserType)?.LOV_Desc;

			return View(CommonViewModel);
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
				CommonViewModel.SelectListItems.AddRange(listUOM.Select(x => new SelectListItem_Custom(x.LOV_Code, x.LOV_Desc, x.DisplayOrder, x.ProjectDetailTypeAccess, x.LOV_Column)).ToList());

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
						CommonViewModel.Message = "Please enter Username.";

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
						CommonViewModel.Message = "Please enter Firstname.";

						return Json(CommonViewModel);
					}

					if (string.IsNullOrEmpty(viewModel.LastName))
					{
						CommonViewModel.IsSuccess = false;
						CommonViewModel.StatusCode = ResponseStatusCode.Error;
						CommonViewModel.Message = "Please enter Lastname.";

						return Json(CommonViewModel);
					}

					if (string.IsNullOrEmpty(viewModel.UserType))
					{
						CommonViewModel.IsSuccess = false;
						CommonViewModel.StatusCode = ResponseStatusCode.Error;
						CommonViewModel.Message = "Please select Designation.";

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
		//[CustomAuthorizeAttribute(AccessType_Enum.Delete)]
		public ActionResult DeleteConfirmed(long Id)
		{
			try
			{
				//if (Common.IsAdmin() && !_context.Using<UserRoleMapping>().Any(x => x.EmployeeId == Id)
				//	&& _context.Employees.Any(x => x.Id > 1 && x.Id == Id))
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
			catch (Exception ex) { }

			CommonViewModel.IsSuccess = false;
			CommonViewModel.StatusCode = ResponseStatusCode.Error;
			CommonViewModel.Message = ResponseStatusMessage.Unable_Delete;

			return Json(CommonViewModel);
		}


	}

}