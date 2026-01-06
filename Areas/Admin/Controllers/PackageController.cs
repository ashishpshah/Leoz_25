using Leoz_25.Controllers;
using Leoz_25.Infra;
using Microsoft.AspNetCore.Mvc;
namespace Leoz_25.Areas.Admin.Controllers
{
	[Area("Admin")]
	public class PackageController : BaseController<ResponseModel<Package>>
	{
		public PackageController(IRepositoryWrapper repository) : base(repository) { }

		// GET: Admin/Package
		public ActionResult Index()
		{
			CommonViewModel.ObjList = _context.Using<Package>().GetByCondition(x => x.VendorId == Logged_In_VendorId).ToList();

			return View(CommonViewModel);
		}

		//[CustomAuthorizeAttribute(AccessType_Enum.Read)]
		public ActionResult Partial_AddEditForm(long Id = 0)
		{
			CommonViewModel.Obj = new Package();

			if (Id > 0) CommonViewModel.Obj = _context.Using<Package>().GetByCondition(x => x.VendorId == Logged_In_VendorId && x.Id == Id).FirstOrDefault();

			return PartialView("_Partial_AddEditForm", CommonViewModel);
		}

		[HttpPost]
		//[CustomAuthorizeAttribute(AccessType_Enum.Write)]
		public ActionResult Save(Package viewModel)
		{
			try
			{
				if (viewModel != null && viewModel != null)
				{
					#region Validation

					if (string.IsNullOrEmpty(viewModel.Name))
					{
						CommonViewModel.IsSuccess = false;
						CommonViewModel.StatusCode = ResponseStatusCode.Error;
						CommonViewModel.Message = "Please enter Package name.";

						return Json(CommonViewModel);
					}

					if (_context.Using<Package>().GetAll().ToList().Any(x => x.VendorId == Logged_In_VendorId && x.Name.ToLower().Replace(" ", "") == viewModel.Name.ToLower().Replace(" ", "") && x.Id != viewModel.Id))
					{
						CommonViewModel.IsSuccess = false;
						CommonViewModel.StatusCode = ResponseStatusCode.Error;
						CommonViewModel.Message = "Package name already exist. Please try another Package name.";

						return Json(CommonViewModel);
					}

					#endregion

					#region Database-Transaction

					//using (var transaction = _context.Database.BeginTransaction())
					//{
					try
					{
						viewModel.IsYearly = false;
						viewModel.IsProjectBased = false;

						if (viewModel.Option == "IsYearly")
						{
							viewModel.IsYearly = true;
							viewModel.ProjectLimit = 0;
						}
						else if (viewModel.Option == "IsProjectBased")
						{
							viewModel.IsProjectBased = true;
							viewModel.DurationInDays = 0;
						}

						Package obj = _context.Using<Package>().GetByCondition(x => x.VendorId == Logged_In_VendorId && x.Id == viewModel.Id).FirstOrDefault();

						if (obj != null)
						{
							obj.VendorId = Logged_In_VendorId;

							obj.Name = viewModel.Name;
							obj.Description = viewModel.Description;
							obj.Price = viewModel.Price;

							obj.IsYearly = viewModel.IsYearly;
							obj.DurationInDays = viewModel.DurationInDays;

							obj.IsProjectBased = viewModel.IsProjectBased;
							obj.ProjectLimit = viewModel.ProjectLimit;

							obj.IsActive = viewModel.IsActive;

							_context.Using<Package>().Update(obj);
							//_context.Entry(obj).State = EntityState.Modified;
							//_context.SaveChanges();
						}
						else
						{
							viewModel.VendorId = Logged_In_VendorId;

							_context.Using<Package>().Add(viewModel);
							//_context.SaveChanges();
						}

						CommonViewModel.IsConfirm = true;
						CommonViewModel.IsSuccess = true;
						CommonViewModel.StatusCode = ResponseStatusCode.Success;
						CommonViewModel.Message = ResponseStatusMessage.Success;
						CommonViewModel.RedirectURL = Url.Action("Index", "Package", new { area = "Admin" });

						//transaction.Commit();

						return Json(CommonViewModel);
					}
					catch (Exception ex) { LogService.LogInsert(GetCurrentAction(), "", ex); }
					//}

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
				if (_context.Using<Package>().Any(x => x.VendorId == Logged_In_VendorId && x.Id == Id))
				{
					var obj = _context.Using<Package>().GetByCondition(x => x.VendorId == Logged_In_VendorId && x.Id == Id).FirstOrDefault();

					obj.IsActive = false;
					obj.IsDeleted = true;

					_context.Using<Package>().Update(obj);
					//_context.Entry(obj).State = EntityState.Deleted;
					//_context.SaveChanges();

					CommonViewModel.IsConfirm = true;
					CommonViewModel.IsSuccess = true;
					CommonViewModel.StatusCode = ResponseStatusCode.Success;
					CommonViewModel.Message = ResponseStatusMessage.Delete;

					CommonViewModel.RedirectURL = Url.Action("Index", "Package", new { area = "Admin" });

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