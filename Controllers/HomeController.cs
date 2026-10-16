using Leoz_25.Infra;
using Leoz_25.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NuGet.Protocol.Core.Types;
using System.Diagnostics;

namespace Leoz_25.Controllers
{
	public class HomeController : BaseController<ResponseModel<LoginViewModel>>
	{
		public HomeController(IRepositoryWrapper repository) : base(repository) { }

		public IActionResult Index()
		{
			// First run (empty database) : create default Super Admin login admin@leoz.com / admin, roles and configuration menus
			var list = _context.Using<User>().GetAll().ToList();

			var (IsSuccess, Message, Id) = (false, "", (long)0);

			if (list == null || list.Count == 0)
			{
				Common.Set_Session_Int(SessionKey.KEY_USER_ID, 1);

				var user = new User() { UserName = "admin@leoz.com", Password = Common.Encrypt("admin"), CreatedBy = 1 };
				user = _context.Using<User>().Add(user);
				//_context.SaveChanges();
				//_context.Entry(user).Reload();

				var role = new Role() { Name = "Super Admin", IsAdmin = true, CreatedBy = 1 };
				role = _context.Using<Role>().Add(role);
				//_context.SaveChanges();

				var userRole = new UserRoleMapping() { UserId = user.Id, RoleId = role.Id, CreatedBy = 1 };
				_context.Using<UserRoleMapping>().Add(userRole);
				//_context.SaveChanges();

				role = new Role() { Name = "Admin", IsAdmin = true, CreatedBy = 1 };
				role = _context.Using<Role>().Add(role);
				//_context.SaveChanges();
				//_context.Entry(role).Reload();

				var menu = new Menu() { ParentId = 0, Area = "", Controller = "", Name = "Configuration", IsSuperAdmin = false, IsAdmin = true, DisplayOrder = 1, CreatedBy = 1 };
				menu = _context.Using<Menu>().Add(menu);
				//_context.SaveChanges();
				//if (menu.Id <= 0) _context.Entry(menu).Reload();

				List<Menu> listMenu_Child = new List<Menu>();

				listMenu_Child.Add(new Menu() { ParentId = menu.Id, Area = "Admin", Controller = "User", Name = "User", IsSuperAdmin = false, IsAdmin = true, DisplayOrder = 2, CreatedBy = 1 });
				listMenu_Child.Add(new Menu() { ParentId = menu.Id, Area = "Admin", Controller = "Role", Name = "Role", IsSuperAdmin = false, IsAdmin = true, DisplayOrder = 3, CreatedBy = 1 });
				listMenu_Child.Add(new Menu() { ParentId = menu.Id, Area = "Admin", Controller = "Access", Name = "User Access", IsSuperAdmin = false, IsAdmin = true, DisplayOrder = 4, CreatedBy = 1 });
				listMenu_Child.Add(new Menu() { ParentId = menu.Id, Area = "Admin", Controller = "Menu", Name = "Menu", IsSuperAdmin = true, IsAdmin = false, DisplayOrder = 5, CreatedBy = 1 });

				for (int i = 0; i < listMenu_Child.Count; i++)
				{
					listMenu_Child[i] = _context.Using<Menu>().Add(listMenu_Child[i]);
				}
				//foreach (var item in listMenu_Child)
				//{
				//    _context.Using<Menu>().Add(item);
				//    _context.SaveChanges();
				//    if (item.Id <= 0) _context.Entry(item).Reload();
				//}

				foreach (var item in listMenu_Child.OrderBy(x => x.ParentId).ThenBy(x => x.Id).ToList())
				{
					var roleMenuAccess = new RoleMenuAccess() { RoleId = role.Id, MenuId = item.Id, IsCreate = true, IsUpdate = true, IsRead = true, IsDelete = true, CreatedBy = 1 };
					_context.Using<RoleMenuAccess>().Add(roleMenuAccess);
					//_context.SaveChanges();
				}

				// Remove temporary user id (used for audit columns while creating default data)
				Common.Clear_Session();
			}

			if (Common.LoggedUser_Id() <= 0)
				return RedirectToAction("Account", "Home", new { Area = "Admin" });

			// Logged in user : open Admin dashboard (not default ASP.NET template page)
			return RedirectToAction("Index", "Home", new { Area = "Admin" });
		}

		public IActionResult Privacy()
		{
			return View();
		}

		[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
		public IActionResult Error()
		{
			return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
		}
	}
}
