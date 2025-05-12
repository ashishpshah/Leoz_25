using Leoz_25.Infra;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NuGet.Protocol.Core.Types;

namespace Leoz_25.Controllers
{


	public class BaseController : Controller
	{

	}

	public class BaseController<T> : BaseController where T : class
	{
		public T CommonViewModel { get; set; } = default(T);

		public bool IsLogActive = false;

		public readonly DateTime? nullDateTime = null;
		public string ControllerName = "";
		public string ActionName = "";
		public string AreaName = "";

		public long Logged_In_VendorId { get; set; }
		public long Logged_In_CustomerId { get; set; }
		public long Logged_In_Customer_VendorId { get; set; }

		public IRepositoryWrapper _context;

		public BaseController()
		{
			try { Logged_In_VendorId = Common.Get_Session_Int(SessionKey.KEY_IS_VENDOR) == 1 ? Common.Get_Session_Int(SessionKey.KEY_USER_ID) : 0; } catch { }
			try { Logged_In_CustomerId = Common.Get_Session_Int(SessionKey.KEY_IS_CUSTOMER) == 1 ? Common.Get_Session_Int(SessionKey.KEY_USER_ID) : 0; } catch { }
			try { Logged_In_Customer_VendorId = Common.Get_Session_Int(SessionKey.KEY_IS_CUSTOMER_VENDOR_ID); } catch { }
		}

		public BaseController(IRepositoryWrapper repository)
		{
			try { Logged_In_VendorId = Common.Get_Session_Int(SessionKey.KEY_IS_VENDOR) == 1 ? Common.Get_Session_Int(SessionKey.KEY_USER_ID) : 0; } catch { }
			try { Logged_In_CustomerId = Common.Get_Session_Int(SessionKey.KEY_IS_CUSTOMER) == 1 ? Common.Get_Session_Int(SessionKey.KEY_USER_ID) : 0; } catch { }
			try { Logged_In_Customer_VendorId = Common.Get_Session_Int(SessionKey.KEY_IS_CUSTOMER_VENDOR_ID); } catch { }

			_context = repository;
			CommonViewModel = (dynamic)Activator.CreateInstance(typeof(T));
		}

		public override void OnActionExecuting(ActionExecutingContext context)
		{
			try
			{
				ControllerName = Convert.ToString(context.RouteData.Values["controller"]);
				ActionName = Convert.ToString(context.RouteData.Values["action"]);

				if (context.RouteData.DataTokens != null) AreaName = Convert.ToString(context.RouteData.DataTokens["area"]);

				if (string.IsNullOrEmpty(AreaName)) AreaName = Convert.ToString(context.RouteData.Values["area"]);

				List<UserMenuAccess> listMenuAccess = Common.GetUserMenuPermission();

				if (listMenuAccess != null && listMenuAccess.Count > 0)
				{
					if (listMenuAccess.FindIndex(x => x.Controller == ControllerName) > -1)
					{
						//CommonViewModel.IsCreate = listMenuAccess[listMenuAccess.FindIndex(x => x.Controller == ControllerName)].IsCreate;
						//CommonViewModel.IsRead = listMenuAccess[listMenuAccess.FindIndex(x => x.Controller == ControllerName)].IsRead;
						//CommonViewModel.IsUpdate = listMenuAccess[listMenuAccess.FindIndex(x => x.Controller == ControllerName)].IsUpdate;
						//CommonViewModel.IsDelete = listMenuAccess[listMenuAccess.FindIndex(x => x.Controller == ControllerName)].IsDelete;

						try { Common.Set_Session_Int(SessionKey.CURRENT_MENU_ID, listMenuAccess[listMenuAccess.FindIndex(x => x.Controller == ControllerName)].MenuId); }
						catch { Common.Set_Session_Int(SessionKey.CURRENT_MENU_ID, 0); }
					}
				}

				if (!Common.IsUserLogged() && !string.IsNullOrEmpty(AreaName) && !(Convert.ToString(ControllerName).ToLower() == "home" && (new string[] { "account", "login" }).Any(x => x == Convert.ToString(ActionName).ToLower())))
				{
					context.Result = new RedirectResult(Url.Content("~/") + (string.IsNullOrEmpty(AreaName) ? "" : AreaName + "/") + "Home/Account");
					return;
				}
				else if (!Common.IsUserLogged() && !Common.IsAdmin() && !string.IsNullOrEmpty(AreaName) && !(Convert.ToString(ControllerName).ToLower() == "home" && (new string[] { "account", "login" }).Any(x => x == Convert.ToString(ActionName).ToLower())))
				{
					context.Result = new RedirectResult(Url.Content("~/") + "Home/Login");
					return;
				}

			}
			catch (Exception ex) { LogService.LogInsert(GetCurrentAction(), "", ex); }
		}


		public string GetCurrentAction() => (string.IsNullOrEmpty(AreaName) ? "" : AreaName + " - ") + ControllerName + " - " + ActionName;
		public string GetCurrentControllerUrl() => (string.IsNullOrEmpty(AreaName) ? "" : AreaName + "/") + ControllerName;
	}
}
