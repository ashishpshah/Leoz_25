using Leoz_25.Infra;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NuGet.Protocol.Core.Types;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

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

				// "Remember Me" : session expired but the auth cookie is valid -> rebuild the session (not on Logout)
				if (!Common.IsUserLogged() && context.HttpContext.User?.Identity?.IsAuthenticated == true && !string.Equals(ActionName, "Logout", StringComparison.OrdinalIgnoreCase))
					Restore_User_Session(context.HttpContext);

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

		private const string CLAIM_SECURITY_STAMP = "leoz:stamp";

		/// <summary>
		/// Issues the auth cookie. It holds NO credentials : only the user id, user name and a one-way security stamp
		/// (changes when the password changes, so old "Remember Me" cookies stop working). The cookie itself is encrypted by Data Protection.
		/// RememberMe = persistent cookie, 14 days sliding; otherwise a browser-session cookie with the session idle timeout.
		/// </summary>
		protected Task SignIn_User(User obj, bool rememberMe)
		{
			var identity = new ClaimsIdentity(new[]
			{
				new Claim(ClaimTypes.NameIdentifier, obj.Id.ToString()),
				new Claim(ClaimTypes.Name, obj.UserName ?? ""),
				new Claim(CLAIM_SECURITY_STAMP, Get_Security_Stamp(obj))
			}, CookieAuthenticationDefaults.AuthenticationScheme);

			var idleMinutes = HttpContext.RequestServices.GetRequiredService<IConfiguration>().GetValue<int?>("DistributedSession:IdleTimeoutMinutes") ?? 30;

			var properties = new AuthenticationProperties
			{
				IsPersistent = rememberMe,
				AllowRefresh = true,
				// RememberMe : ExpireTimeSpan from Program.cs (14 days, sliding). Otherwise same idle timeout as the session.
				ExpiresUtc = rememberMe ? null : DateTimeOffset.UtcNow.AddMinutes(idleMinutes)
			};

			return HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity), properties);
		}

		protected Task SignOut_User() => HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

		private static string Get_Security_Stamp(User obj) =>
			Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(obj.Id + "|" + obj.UserName + "|" + obj.Password)));

		/// <summary>
		/// Session expired but the auth cookie is still valid ("Remember Me") : re-check the user in the database and rebuild the session.
		/// Inactive / deleted user or changed password : the cookie is removed instead.
		/// </summary>
		private void Restore_User_Session(HttpContext httpContext)
		{
			var principal = httpContext.User;

			long.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out long userId);

			var obj = userId > 0 ? _context.Using<User>().GetByCondition(x => x.Id == userId).FirstOrDefault() : null;

			bool isValid = obj != null && obj.IsActive == true && obj.IsDeleted == false
				&& string.Equals(principal.FindFirstValue(CLAIM_SECURITY_STAMP), Get_Security_Stamp(obj), StringComparison.Ordinal);

			if (isValid && Load_User_Session(obj!))
			{
				// the constructor read the (empty) session before this ran
				IsVendor = Common.Get_Session_Int(SessionKey.KEY_IS_VENDOR) == 1;
				IsCustomer = Common.Get_Session_Int(SessionKey.KEY_IS_CUSTOMER) == 1;
				IsEmployee = Common.Get_Session_Int(SessionKey.KEY_IS_EMPLOYEE) == 1;
				Logged_In_UserId = Common.Get_Session_Int(SessionKey.KEY_USER_ID);
				Logged_In_VendorId = Common.Get_Session_Int(SessionKey.KEY_VENDOR_ID);
				Logged_In_CustomerId = Common.Get_Session_Int(SessionKey.KEY_CUSTOMER_ID);
				Logged_In_EmployeeId = Common.Get_Session_Int(SessionKey.KEY_EMPLOYEE_ID);
				return;
			}

			Common.Clear_Session();
			httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme).GetAwaiter().GetResult();
		}

		/// <summary>
		/// Fills the session of a verified user (role, menus, vendor / customer / employee ...).
		/// Used by Login and to restore the session from the "Remember Me" cookie. Returns false when the user has no role.
		/// </summary>
		protected bool Load_User_Session(User obj)
		{
			var userRole = _context.Using<UserRoleMapping>().GetByCondition(x => x.UserId == obj.Id).FirstOrDefault();

			obj.RoleId = userRole != null ? userRole.RoleId : 0;

			List<UserMenuAccess> listMenuAccess = new List<UserMenuAccess>();
			List<UserMenuAccess> listMenuPermission = new List<UserMenuAccess>();

			Role role = _context.Using<Role>().GetByCondition(x => x.Id == obj.RoleId).FirstOrDefault();

			if (role == null)
				return false;
			else if (role != null && role.Id == 1)
			{
				listMenuAccess = (from y in _context.Using<Menu>().GetAll().ToList()
								  where y.IsActive == true && y.IsDeleted == false
								  select new UserMenuAccess() { Id = y.Id, ParentMenuId = y.ParentId, Area = y.Area, Controller = y.Controller, Url = y.Url, MenuName = y.Name, Icon = y.Icon,IsCreate = true, IsUpdate = true, IsRead = true, IsDelete = true, DisplayOrder = y.DisplayOrder, IsActive = y.IsActive, IsDeleted = y.IsDeleted }).ToList();
			}
			else if (role != null && (role.IsAdmin || role.Name.ToUpper() == "VENDOR"))
			{
				listMenuAccess = (from x in _context.Using<UserMenuAccess>().GetAll().ToList()
								  join y in _context.Using<Menu>().GetAll().ToList() on x.MenuId equals y.Id
								  where x.UserId == obj.Id && x.RoleId == obj.RoleId
								  && y.IsActive == true && y.IsDeleted == false && x.IsActive == true && x.IsDeleted == false && y.Name != "Menu"
								  && x.IsRead == true
								  select new UserMenuAccess() { Id = y.Id, ParentMenuId = y.ParentId, Area = y.Area, Controller = y.Controller, Url = y.Url, MenuName = y.Name, DisplayOrder = y.DisplayOrder, Icon = y.Icon, IsActive = x.IsActive, IsDeleted = x.IsDeleted }).ToList();
			}
			else if (role != null && !role.IsAdmin && role.IsActive && !role.IsDeleted)
			{
				listMenuAccess = (from x in _context.Using<UserMenuAccess>().GetAll().ToList()
								  join y in _context.Using<Menu>().GetAll().ToList() on x.MenuId equals y.Id
								  where x.UserId == obj.Id && x.RoleId == obj.RoleId
								  && y.IsActive == true && y.IsDeleted == false && x.IsActive == true && x.IsDeleted == false && y.Id != 1 && y.ParentId != 1 && y.Name != "Menu"
								  && x.IsRead == true
								  select new UserMenuAccess() { Id = y.Id, ParentMenuId = y.ParentId, Area = y.Area, Controller = y.Controller, Url = y.Url, MenuName = y.Name, DisplayOrder = y.DisplayOrder,Icon = y.Icon ,IsActive = x.IsActive, IsDeleted = x.IsDeleted }).ToList();
			}

			if (role != null && role.Id == 1)
				listMenuPermission = listMenuAccess;
			else
				listMenuPermission = (from x in _context.Using<UserMenuAccess>().GetAll().ToList()
									  join y in _context.Using<Menu>().GetAll().ToList() on x.MenuId equals y.Id
									  where x.UserId == obj.Id && y.IsActive == true && y.IsDeleted == false && x.IsActive == true && x.IsDeleted == false
									  && listMenuAccess.Any(z => z.Id == y.Id)
									  select new UserMenuAccess() { MenuId = y.Id, ParentMenuId = y.ParentId, Area = y.Area, Controller = y.Controller, Url = y.Url, MenuName = y.Name,Icon = y.Icon ,IsCreate = x.IsCreate, IsUpdate = x.IsUpdate, IsRead = x.IsRead, IsDelete = x.IsDelete, IsActive = x.IsActive, IsDeleted = x.IsDeleted }).ToList();

			Common.Configure_UserMenuAccess(listMenuAccess.Where(x => x.IsActive == true && x.IsDeleted == false).ToList(), listMenuPermission.Where(x => x.IsActive == true && x.IsDeleted == false).ToList());

			Common.Set_Session_Int(SessionKey.KEY_USER_ID, obj.Id);
			Common.Set_Session_Int(SessionKey.KEY_USER_ROLE_ID, obj.RoleId);

			Common.Set_Session(SessionKey.KEY_USER_NAME, obj.UserName);
			Common.Set_Session(SessionKey.KEY_USER_ROLE, role.Name);
			Common.Set_Session_Int(SessionKey.KEY_IS_ADMIN, (role.IsAdmin || obj.RoleId == 1 ? 1 : 0));
			Common.Set_Session_Int(SessionKey.KEY_IS_VENDOR, (role.Name.ToUpper() == "VENDOR" ? 1 : 0));
			Common.Set_Session_Int(SessionKey.KEY_IS_CUSTOMER, (role.Name.ToUpper() == "CUSTOMER" ? 1 : 0));
			Common.Set_Session_Int(SessionKey.KEY_IS_EMPLOYEE, (role.Name.ToUpper() == "EMPLOYEE" ? 1 : 0));
			Common.Set_Session_Int(SessionKey.KEY_IS_SUPER_USER, (obj.RoleId == 1 ? 1 : 0));

			long VendorId = 0;

			if (role.Name.ToUpper() == "VENDOR")
			{
				var vendor = _context.Using<Vendor>().GetByCondition(x => x.UserId == obj.Id).FirstOrDefault();
				VendorId = vendor != null ? vendor.Id : 0;

				Common.Set_Session(SessionKey.ProjectDetailTypeAccess, "ALL");
			}
			else if (role.Name.ToUpper() == "CUSTOMER")
			{
				VendorId = 0;

				var customer = _context.Using<Customer>().GetByCondition(x => x.UserId == obj.Id).FirstOrDefault();
				VendorId = customer != null ? customer.VendorId : 0;
				Common.Set_Session_Int(SessionKey.KEY_CUSTOMER_ID, customer != null ? customer.Id : 0);

				Common.Set_Session(SessionKey.ProjectDetailTypeAccess, role.ProjectDetailTypeAccess ?? "");
			}
			else if (role.Name.ToUpper() == "EMPLOYEE")
			{
				VendorId = 0;

				var employee = _context.Using<Employee>().GetByCondition(x => x.UserId == obj.Id).FirstOrDefault();
				VendorId = employee != null ? employee.VendorId : 0;
				Common.Set_Session_Int(SessionKey.KEY_EMPLOYEE_ID, employee != null ? employee.Id : 0);

				if (employee != null)
				{
					var objUserType = _context.Using<LOV>().GetByCondition(x => x.LOV_Column == "USER_TYPE" && x.LOV_Code == employee.UserType).FirstOrDefault();

					Common.Set_Session(SessionKey.ProjectDetailTypeAccess, objUserType?.ProjectDetailTypeAccess ?? "");
					Common.Set_Session("Employee_UserType", employee.UserType);
					Common.Set_Session(ProjectAccess.KEY_DESIGNATION, ProjectAccess.ResolveDesignation(objUserType, employee.UserType));
					Common.Set_Session(ProjectAccess.KEY_DESIGNATION_NAME, objUserType?.LOV_Desc ?? employee.UserType ?? "");
				}
			}

			Common.Set_Session_Int(SessionKey.KEY_VENDOR_ID, VendorId);

			// Project Detail module permissions (database-driven) of this subject / tenant
			HttpContext.RequestServices.GetRequiredService<IPermissionService>().RefreshSession(force: true);

			return true;
		}

		public string GetCurrentAction() => (string.IsNullOrEmpty(AreaName) ? "" : AreaName + " - ") + ControllerName + " - " + ActionName;
		public string GetCurrentControllerUrl() => (string.IsNullOrEmpty(AreaName) ? "" : AreaName + "/") + ControllerName;
	}
}
