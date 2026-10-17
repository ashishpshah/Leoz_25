using Leoz_25.Controllers;
using Leoz_25.Infra;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Leoz_25.Areas.Admin.Controllers
{
	[Area("Admin")]
	public class HomeController : BaseController<ResponseModel<LoginViewModel>>
	{
		public HomeController(IRepositoryWrapper repository) : base(repository) { }

		public ActionResult Index()
		{
			if (Common.LoggedUser_Id() <= 0)
				return RedirectToAction("Account", "Home", new { Area = "Admin" });

			return View();
		}

		public async Task<ActionResult> Account()
		{
			// already logged in (or restored from the "Remember Me" cookie in BaseController) : go to the dashboard
			if (Common.IsUserLogged())
				return RedirectToAction("Index", "Home", new { Area = "Admin" });

			Common.Clear_Session();
			await SignOut_User(); // removes a stale / invalid auth cookie

			return View(new ResponseModel<LoginViewModel>());
		}


		[HttpPost]
		//[ValidateAntiForgeryToken]
		public async Task<JsonResult> Login(LoginViewModel viewModel)
		{
			try
			{
				if (!string.IsNullOrEmpty(viewModel.UserName) && viewModel.UserName.Length > 0 && _context.Using<User>().GetAll().ToList().Any(x => x.UserName == viewModel.UserName))
				{
					viewModel.Password = Common.Encrypt(viewModel.Password);

					var obj = _context.Using<User>().GetByCondition(x => x.UserName == viewModel.UserName && x.Password == viewModel.Password).FirstOrDefault();

					if (obj != null && obj.IsActive == true && obj.IsDeleted == false)
					{
						// session (role, menus ...) : BaseController.Load_User_Session, also used to restore a "Remember Me" login
						if (!Load_User_Session(obj))
						{
							CommonViewModel.IsSuccess = false;
							CommonViewModel.StatusCode = ResponseStatusCode.Error;
							CommonViewModel.Message = ResponseStatusMessage.Error;

							return Json(CommonViewModel);
						}

						// auth cookie : persistent for 14 days (sliding) only when "Remember Me" is ticked
						await SignIn_User(obj, viewModel.RememberMe);

						CommonViewModel.IsSuccess = true;
						CommonViewModel.StatusCode = ResponseStatusCode.Success;
						CommonViewModel.Message = ResponseStatusMessage.Success;

						CommonViewModel.RedirectURL = Url.Content("~/") + "Admin/" + this.ControllerContext.RouteData.Values["Controller"].ToString() + "/Index";

						return Json(CommonViewModel);
					}

				}

				CommonViewModel.IsSuccess = false;
				CommonViewModel.StatusCode = ResponseStatusCode.Error;
				CommonViewModel.Message = "User Id and Password does not Match";

			}
			catch (Exception ex)
			{
				LogService.LogInsert(GetCurrentAction(), "", ex);
				CommonViewModel.IsSuccess = false;
				CommonViewModel.StatusCode = ResponseStatusCode.Error;
				CommonViewModel.Message = ResponseStatusMessage.Error + " | " + ex.Message;
			}

			return Json(CommonViewModel);
		}

		public async Task<ActionResult> Logout()
		{
			Common.Clear_Session();
			await SignOut_User(); // deletes the auth ("Remember Me") cookie

			return RedirectToAction("Account", "Home", new { Area = "Admin" });
		}

	}
}