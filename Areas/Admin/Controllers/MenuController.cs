using Leoz_25.Controllers;
using Leoz_25.Infra;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Leoz_25.Areas.Admin.Controllers
{
	[Area("Admin")]
	public class MenuController : BaseController<ResponseModel<Menu>>
	{
		public MenuController(IRepositoryWrapper repository) : base(repository) { }

		// GET: Admin/Menu
		public ActionResult Index()
		{
			var list = new List<Menu>();

			if (Common.IsSuperAdmin())
			{
				list = _context.Using<Menu>().GetAll().ToList();

				if (Common.IsSuperAdmin())
					CommonViewModel.ObjList = list;

				foreach (Menu obj in list)
					obj.ParentMenuName = list.Where(x => x.Id == obj.ParentId).Select(x => x.Name).FirstOrDefault();

			}

			CommonViewModel.ObjList = list;

			return View(CommonViewModel);
		}

		//[CustomAuthorizeAttribute(AccessType_Enum.Read)]
		public ActionResult Partial_AddEditForm(long Id = 0)
		{
			CommonViewModel.Obj = new Menu();

			if (Common.IsSuperAdmin())
			{
				var list = _context.Using<Menu>().GetAll().ToList();

				if (Common.IsSuperAdmin() && Id > 0)
					CommonViewModel.Obj = list.Where(x => x.Id == Id).FirstOrDefault();

				CommonViewModel.SelectListItems = list.Where(x => x.IsActive == true && x.IsDeleted == false).ToList()
					.Select(x => new SelectListItem_Custom(Convert.ToString(x.Id), Convert.ToString(x.Name))).ToList();
			}

			return PartialView("_Partial_AddEditForm", CommonViewModel);
		}

		[HttpPost]
		//[CustomAuthorizeAttribute(AccessType_Enum.Write)]
		public ActionResult Save(Menu viewModel)
		{
			try
			{
				if (viewModel != null && viewModel != null)
				{
					#region Validation

					if (!Common.IsSuperAdmin())
					{
						CommonViewModel.IsSuccess = false;
						CommonViewModel.StatusCode = ResponseStatusCode.Error;
						CommonViewModel.Message = ResponseStatusMessage.UnAuthorize;

						return Json(CommonViewModel);
					}

					if (string.IsNullOrEmpty(viewModel.Name))
					{
						CommonViewModel.IsSuccess = false;
						CommonViewModel.StatusCode = ResponseStatusCode.Error;
						CommonViewModel.Message = "Please enter Menu name.";

						return Json(CommonViewModel);
					}

					if (_context.Using<Menu>().GetAll().ToList().Any(x => x.Name.ToLower().Replace(" ", "") == viewModel.Name.ToLower().Replace(" ", "") && x.Id != viewModel.Id && x.ParentId == viewModel.ParentId))
					{
						CommonViewModel.IsSuccess = false;
						CommonViewModel.StatusCode = ResponseStatusCode.Error;
						CommonViewModel.Message = "Menu already exist. Please try another Menu.";

						return Json(CommonViewModel);
					}

					#endregion

					#region Database-Transaction

					//using (var transaction = _context.Database.BeginTransaction())
					//{
					try
					{
						Menu obj = _context.Using<Menu>().GetByCondition(x => x.Id == viewModel.Id).FirstOrDefault();

						if (viewModel != null && !(viewModel.DisplayOrder > 0))
							viewModel.DisplayOrder = _context.Using<Menu>().GetAll().ToList().OrderByDescending(x => x.DisplayOrder)
																	.Select(x => x.DisplayOrder).FirstOrDefault() + 1;

						if (Common.IsSuperAdmin() && obj != null)
						{
							obj.Name = viewModel.Name;
							obj.ParentId = viewModel.ParentId;
							obj.Area = viewModel.Area;
							obj.Controller = viewModel.Controller;
							obj.Url = viewModel.Url;

							obj.DisplayOrder = viewModel.DisplayOrder;
							obj.IsActive = viewModel.IsActive;

							_context.Using<Menu>().Update(obj);
							//_context.Entry(obj).State = EntityState.Modified;
							//_context.SaveChanges();
						}
						else if (Common.IsSuperAdmin())
						{
							_context.Using<Menu>().Add(viewModel);
							//_context.SaveChanges();
						}

						CommonViewModel.IsConfirm = true;
						CommonViewModel.IsSuccess = true;
						CommonViewModel.StatusCode = ResponseStatusCode.Success;
						CommonViewModel.Message = ResponseStatusMessage.Success;
						CommonViewModel.RedirectURL = Url.Action("Index", "Menu", new { area = "Admin" });

						//transaction.Commit();

						return Json(CommonViewModel);
					}
					catch (Exception ex)
					{ /*transaction.Rollback();*/ }
					//}

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
				if (!Common.IsSuperAdmin())
				{
					CommonViewModel.IsSuccess = false;
					CommonViewModel.StatusCode = ResponseStatusCode.Error;
					CommonViewModel.Message = ResponseStatusMessage.UnAuthorize;

					return Json(CommonViewModel);
				}

				if (Common.IsSuperAdmin() && _context.Using<Menu>().GetAll().ToList().Any(x => x.Id == Id))
				{
					var listUserMenuAccess = _context.Using<UserMenuAccess>().GetByCondition(x => x.MenuId == Id).ToList();

					if (listUserMenuAccess != null && listUserMenuAccess.Count() > 0)
						foreach (var access in listUserMenuAccess) _context.Using<UserMenuAccess>().Delete(access);

					var obj = _context.Using<Menu>().GetByCondition(x => x.Id == Id).FirstOrDefault();

					_context.Using<Menu>().Delete(obj);

					CommonViewModel.IsConfirm = true;
					CommonViewModel.IsSuccess = true;
					CommonViewModel.StatusCode = ResponseStatusCode.Success;
					CommonViewModel.Message = ResponseStatusMessage.Delete;

					CommonViewModel.RedirectURL = Url.Action("Index", "Menu", new { area = "Admin" });

					return Json(CommonViewModel);
				}
			}
			catch (Exception ex)
			{ }

			CommonViewModel.IsSuccess = false;
			CommonViewModel.StatusCode = ResponseStatusCode.Error;
			CommonViewModel.Message = ResponseStatusMessage.Unable_Delete;

			return Json(CommonViewModel);
		}

	}

}