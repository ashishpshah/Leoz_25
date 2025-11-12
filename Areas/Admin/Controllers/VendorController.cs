using Leoz_25.Controllers;
using Leoz_25.Infra;
using Leoz_25.Infra.Services;
using Microsoft.AspNetCore.Mvc;
using System.Data;

namespace Leoz_25.Areas.Admin.Controllers
{
	[Area("Admin")]
	public class VendorController : BaseController<ResponseModel<Vendor>>
	{
		public VendorController(IRepositoryWrapper repository) : base(repository) { }

		// GET: Admin/Vendor
		public ActionResult Index()
		{
			var list = DataContext_Command.Vendor_Get(0).ToList();

			CommonViewModel.ObjList = DataContext_Command.Vendor_Get(0).Where(x => IsVendor && x.CreatedBy == Logged_In_UserId).ToList();

			return View(CommonViewModel);
		}

		public ActionResult Users()
		{
			//CommonViewModel.ObjList = DataContext_Command.Vendor_Get(0).ToList();

			return View(CommonViewModel);
		}

		//[CustomAuthorizeAttribute(AccessType_Enum.Read)]
		public ActionResult Partial_AddEditForm(long Id = 0)
		{
			CommonViewModel.Obj = new Vendor() { };

			var list = DataContext_Command.Vendor_Get(Id).ToList();

			if (Id > 0) CommonViewModel.Obj = list.Where(x => IsVendor && x.CreatedBy == Logged_In_UserId).FirstOrDefault();

			if (CommonViewModel.Obj != null && CommonViewModel.Obj.UserId > 0)
			{
				var obj = _context.Using<User>().GetByCondition(x => x.Id == CommonViewModel.Obj.UserId).FirstOrDefault();

				if (obj != null && obj.IsActive == true && obj.IsDeleted == false)
					CommonViewModel.Obj.UserName = obj.UserName;
			}

			CommonViewModel.SelectListItems = new List<SelectListItem_Custom>();

			var listRole = (from x in _context.Using<Role>().GetAll().ToList()
							where x.IsActive == true && x.Id > 1
							orderby x.Name
							select x).Distinct().ToList();

			if (CommonViewModel.SelectListItems == null) CommonViewModel.SelectListItems = new List<SelectListItem_Custom>();

			if (listRole != null && listRole.Count > 0)
			{
				listRole = listRole.GroupBy(x => new { Id = x.Id, Name = x.Name }).Select(x => new Role() { Id = x.Key.Id, Name = x.Key.Name }).ToList();
				CommonViewModel.SelectListItems.AddRange(listRole.Select(x => new SelectListItem_Custom(x.Id.ToString(), x.Name, "R")).ToList());
			}

			return PartialView("_Partial_AddEditForm", CommonViewModel);
		}

		[HttpPost]
		//[CustomAuthorizeAttribute(AccessType_Enum.Write)]
		public ActionResult Save(Vendor viewModel)
		{
			try
			{
				if (viewModel != null && viewModel != null && (Common.IsAdmin() || IsVendor))
				{
					#region Validation

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

					if (!string.IsNullOrEmpty(viewModel.Email) && !ValidateField.IsValidEmail(viewModel.Email))
					{
						CommonViewModel.IsSuccess = false;
						CommonViewModel.StatusCode = ResponseStatusCode.Error;
						CommonViewModel.Message = "Please enter valid Email.";

						return Json(CommonViewModel);
					}

					if (!string.IsNullOrEmpty(viewModel.ContactNo) && !ValidateField.IsValidMobileNo(viewModel.ContactNo))
					{
						CommonViewModel.IsSuccess = false;
						CommonViewModel.StatusCode = ResponseStatusCode.Error;
						CommonViewModel.Message = "Please enter valid Contact No.";

						return Json(CommonViewModel);
					}

					if (!string.IsNullOrEmpty(viewModel.ContactNo_Alternate) && !ValidateField.IsValidMobileNo(viewModel.ContactNo_Alternate))
					{
						CommonViewModel.IsSuccess = false;
						CommonViewModel.StatusCode = ResponseStatusCode.Error;
						CommonViewModel.Message = "Please enter valid Alternate Contact No.";

						return Json(CommonViewModel);
					}

					#endregion

					#region Database-Transaction

					try
					{
						if (viewModel.IsPassword_Reset == true) viewModel.Password = "12345";

						if (!string.IsNullOrEmpty(viewModel.Password)) viewModel.Password = Common.Encrypt(viewModel.Password);

						var (IsSuccess, response, Id) = DataContext_Command.Vendor_Save(viewModel);
						viewModel.Id = viewModel.Id <= 0 ? Id : viewModel.Id;

						var files = AppHttpContextAccessor.AppHttpContext.Request.Form.Files;

						if (files != null && files.Count() > 0 && files[0].Length > 0)
						{
							byte[] fileBytes;
							using (var memoryStream = new MemoryStream())
							{
								files[0].CopyTo(memoryStream);
								fileBytes = memoryStream.ToArray();
							}

							Vendor obj = _context.Using<Vendor>().GetByCondition(x => x.Id == viewModel.Id).FirstOrDefault();

							if (obj != null)
							{
								obj.Logo = fileBytes;

								_context.Using<Vendor>().Update(obj);
							}
						}

						CommonViewModel.IsConfirm = IsSuccess;
						CommonViewModel.IsSuccess = IsSuccess;
						CommonViewModel.StatusCode = IsSuccess ? ResponseStatusCode.Success : ResponseStatusCode.Error;
						CommonViewModel.Message = response;
						CommonViewModel.RedirectURL = Url.Action("Index", "Vendor", new { area = "Admin" });

						return Json(CommonViewModel);
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
				var (IsSuccess, response) = DataContext_Command.Vendor_Status(Id, false, true);

				CommonViewModel.IsConfirm = IsSuccess;
				CommonViewModel.IsSuccess = IsSuccess;
				CommonViewModel.StatusCode = IsSuccess ? ResponseStatusCode.Success : ResponseStatusCode.Error;
				CommonViewModel.Message = response;
				CommonViewModel.RedirectURL = Url.Action("Index", "Vendor", new { area = "Admin" });

				return Json(CommonViewModel);
			}
			catch (Exception ex) { }

			CommonViewModel.IsSuccess = false;
			CommonViewModel.StatusCode = ResponseStatusCode.Error;
			CommonViewModel.Message = ResponseStatusMessage.Unable_Delete;

			return Json(CommonViewModel);
		}

	}

}