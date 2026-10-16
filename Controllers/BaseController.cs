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

		public bool IsVendor { get; set; }
		public bool IsCustomer { get; set; }
		public bool IsEmployee { get; set; }

		public long Logged_In_UserId { get; set; }
		public long Logged_In_VendorId { get; set; }
		public long Logged_In_CustomerId { get; set; }
		public long Logged_In_EmployeeId { get; set; }

		public IRepositoryWrapper _context;

		public BaseController()
		{
			try { IsVendor = Common.Get_Session_Int(SessionKey.KEY_IS_VENDOR) == 1; } catch { }
			try { IsCustomer = Common.Get_Session_Int(SessionKey.KEY_IS_CUSTOMER) == 1; } catch { }
			try { IsEmployee = Common.Get_Session_Int(SessionKey.KEY_IS_EMPLOYEE) == 1; } catch { }

			try { Logged_In_UserId = Common.Get_Session_Int(SessionKey.KEY_USER_ID); } catch { }
			try { Logged_In_VendorId = Common.Get_Session_Int(SessionKey.KEY_VENDOR_ID); } catch { }
			try { Logged_In_CustomerId = Common.Get_Session_Int(SessionKey.KEY_CUSTOMER_ID); } catch { }
			try { Logged_In_EmployeeId = Common.Get_Session_Int(SessionKey.KEY_EMPLOYEE_ID); } catch { }

		}

		public BaseController(IRepositoryWrapper repository)
		{
			try { IsVendor = Common.Get_Session_Int(SessionKey.KEY_IS_VENDOR) == 1; } catch { }
			try { IsCustomer = Common.Get_Session_Int(SessionKey.KEY_IS_CUSTOMER) == 1; } catch { }
			try { IsEmployee = Common.Get_Session_Int(SessionKey.KEY_IS_EMPLOYEE) == 1; } catch { }

			try { Logged_In_UserId = Common.Get_Session_Int(SessionKey.KEY_USER_ID); } catch { }
			try { Logged_In_VendorId = Common.Get_Session_Int(SessionKey.KEY_VENDOR_ID); } catch { }
			try { Logged_In_CustomerId = Common.Get_Session_Int(SessionKey.KEY_CUSTOMER_ID); } catch { }
			try { Logged_In_EmployeeId = Common.Get_Session_Int(SessionKey.KEY_EMPLOYEE_ID); } catch { }

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

				// Project Detail module permissions : rebuild when PermissionVersion changed (admin edit applies without re-login)
				if (Common.IsUserLogged() && ProjectAccess.Mode != PermissionModes.Legacy)
					context.HttpContext.RequestServices.GetService<IPermissionService>()?.RefreshSession();

			}
			catch (Exception ex) { LogService.LogInsert(GetCurrentAction(), "", ex); }
		}


		/// <summary>
		/// Vendor must have an active subscription before creating Employee / Customer / Project.
		/// Returns error message, or null when subscription is valid.
		/// </summary>
		public string Get_Subscription_Error(bool IsNewProject = false)
		{
			var subscription = _context.Using<VendorSubscription>().GetByCondition(x => x.VendorId == Logged_In_VendorId).OrderByDescending(x => x.Id).FirstOrDefault();

			if (subscription == null) return "You are not subscribe any plan. Please subscribe a plan first.";

			if (subscription.IsCancelled == true) return "Subscribe plan is Cancelled. Please contact administration.";

			if (subscription.IsActive == false) return "You are already subscribe a plan. but plan not active. Please contact administration.";

			var package = _context.Using<Package>().GetByCondition(x => x.Id == subscription.PackageId).FirstOrDefault();

			if (package == null) return "Subscribed plan is not available. Please contact administration.";

			if (package.IsYearly == true && (subscription.StartDate.Date > DateTime.Now.Date || (subscription.EndDate ?? DateTime.MinValue).Date < DateTime.Now.Date))
				return "Your subscription plan is expired. Please subscribe a plan.";

			if (IsNewProject && package.IsProjectBased == true && package.ProjectLimit > 0
				&& Get_Subscription_ProjectCount(subscription) >= package.ProjectLimit)
				return $"You are already used plan Project Limit ({package.ProjectLimit}). Please subscribe a plan.";

			return null;
		}

		/// <summary> Projects created under given subscription. </summary>
		public int Get_Subscription_ProjectCount(VendorSubscription subscription)
			=> subscription == null ? 0 : _context.Using<Project>().GetByCondition(x => x.VendorId == subscription.VendorId && x.CreatedDate >= subscription.CreatedDate).Count();

		public string GetCurrentAction() => (string.IsNullOrEmpty(AreaName) ? "" : AreaName + " - ") + ControllerName + " - " + ActionName;
		public string GetCurrentControllerUrl() => (string.IsNullOrEmpty(AreaName) ? "" : AreaName + "/") + ControllerName;
	}
}
