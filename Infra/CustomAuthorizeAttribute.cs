using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Leoz_25.Infra
{
    public class CustomAuthorizeAttribute : TypeFilterAttribute
    {
        public CustomAuthorizeAttribute(AccessType_Enum claimValue) : base(typeof(CustomAuthorizeFilter))
        {
            Arguments = new object[] { claimValue };
        }
    }
    public class CustomAuthorizeFilter : IAuthorizationFilter
    {
        private readonly AccessType_Enum _permission;

        public CustomAuthorizeFilter(AccessType_Enum permission)
        {
            this._permission = permission;
        }

        public void OnAuthorization(AuthorizationFilterContext filterContext)
        {
            //try
            //{
            //	var descriptor = filterContext?.ActionDescriptor as ControllerActionDescriptor;

            //	string currentAction = descriptor.ActionName;
            //	string currentController = descriptor.ControllerName;
            //	string currentArea = Convert.ToString(descriptor.RouteValues["area"]);

            //	if (currentArea == "Admin" && currentController == "Home")
            //	{
            //		filterContext.Result = new RedirectResult("/Home/Index");
            //		return;
            //	}

            //	if (string.IsNullOrEmpty(AppHttpContextAccessor.AppSession.GetString(Common.USER_ID_KEY))) throw new UnauthorizedAccessException();
            //	//if (AppHttpContextAccessor.GetUserMenuAccesses() == null) throw new UnauthorizedAccessException();
            //	//if (AppHttpContextAccessor.GetUserMenuPermission() == null) throw new UnauthorizedAccessException();

            //	////if (filterContext.HttpContext.Session[Common.AUTH_IS_SUPER_USER_KEY] != null)
            //	////    if (Convert.ToBoolean(filterContext.HttpContext.Session[Common.AUTH_IS_SUPER_USER_KEY]) == true)
            //	////        base.OnAuthorization(filterContext);

            //	//List<UserMenuAccess> listMenuAccess = AppHttpContextAccessor.GetUserMenuPermission();

            //	//if (listMenuAccess != null && listMenuAccess.Count > 0)
            //	//{
            //	//	if (_permission == AccessType_Enum.Read && listMenuAccess.Count(x => x.Controller == currentController && x.IsRead == true) > 0) { return; }
            //	//	else if (_permission == AccessType_Enum.Write && listMenuAccess.Count(x => x.Controller == currentController && x.IsCreate == true) > 0) { return; }
            //	//	else if (_permission == AccessType_Enum.Write && listMenuAccess.Count(x => x.Controller == currentController && x.IsUpdate == true) > 0) { return; }
            //	//	else if (_permission == AccessType_Enum.Delete && listMenuAccess.Count(x => x.Controller == currentController && x.IsDelete == true) > 0) { return; }
            //	//	else throw new UnauthorizedAccessException();
            //	//}

            //	throw new UnauthorizedAccessException();
            //}
            //catch (Exception ex)
            //{
            //	filterContext.Result = new RedirectResult("/Home/Index");
            //	return;
            //}
        }


        //protected override bool AuthorizeCore(HttpContextBase httpContext)
        //{
        //    var rd = httpContext.Request.RequestContext.RouteData;
        //    string currentAction = rd.GetRequiredString("action");
        //    string currentController = rd.GetRequiredString("controller");
        //    string currentArea = rd.DataTokens["area"] as string;

        //    if (httpContext.Session[Common.AUTH_USER_KEY] == null) return false;
        //    if (httpContext.Session[Common.AUTH_USER_MENUACCESS_KEY] == null) return false;
        //    if (httpContext.Session[Common.AUTH_USER_MENU_PERMISSION_KEY] == null) return false;

        //    //if (httpContext.Session[Common.AUTH_IS_SUPER_USER_KEY] != null)
        //    //    if (Convert.ToBoolean(httpContext.Session[Common.AUTH_IS_SUPER_USER_KEY]) == true)
        //    //        return true;

        //    List<UserMenuAccess> listMenuAccess = httpContext.Session[Common.AUTH_USER_MENU_PERMISSION_KEY] as List<UserMenuAccess>;

        //    if (listMenuAccess != null && listMenuAccess.Count > 0)
        //    {
        //        if (_permission == AccessType_Enum.Create && listMenuAccess.Count(x => x.MenuName == currentController && x.IsCreate == true) > 0) return true;
        //        else if (_permission == AccessType_Enum.Read && listMenuAccess.Count(x => x.MenuName == currentController && x.IsRead == true) > 0) return true;
        //        else if (_permission == AccessType_Enum.Update && listMenuAccess.Count(x => x.MenuName == currentController && x.IsUpdate == true) > 0) return true;
        //        else if (_permission == AccessType_Enum.Delete && listMenuAccess.Count(x => x.MenuName == currentController && x.IsDelete == true) > 0) return true;
        //        else return false;
        //    }

        //    return false;
        //}

        //protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
        //{
        //	filterContext.Result = new HttpUnauthorizedResult();
        //}
    }


    public class RedirectMiddleware
    {
        private readonly RequestDelegate _next;

        public RedirectMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var requestPath = context.Request.Path.Value;

            // Check if the path contains `//` after the domain
            if (requestPath.StartsWith("//"))
            {// Replace `//` with `/`
                var newPath = requestPath.Replace("//", "/");

                // Redirect to the modified URL
                context.Response.Redirect(newPath);

                return; // Short-circuit the pipeline
            }

            // If the condition doesn't match, continue with the next middleware
            await _next(context);
        }
    }

}