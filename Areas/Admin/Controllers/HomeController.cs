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

		public ActionResult Account()
		{
			Common.Clear_Session();

			return View(new ResponseModel<LoginViewModel>());
		}


		[HttpPost]
		//[ValidateAntiForgeryToken]
		public JsonResult Login(LoginViewModel viewModel)
		{
			try
			{
				if (!string.IsNullOrEmpty(viewModel.UserName) && viewModel.UserName.Length > 0 && _context.Using<User>().GetAll().ToList().Any(x => x.UserName == viewModel.UserName))
				{
					viewModel.Password = Common.Encrypt(viewModel.Password);

					var obj = _context.Using<User>().GetByCondition(x => x.UserName == viewModel.UserName && x.Password == viewModel.Password).FirstOrDefault();

					if (obj != null && obj.IsActive == true && obj.IsDeleted == false)
					{
						var userRole = _context.Using<UserRoleMapping>().GetByCondition(x => x.UserId == obj.Id).FirstOrDefault();

						obj.RoleId = userRole != null ? userRole.RoleId : 0;

						List<UserMenuAccess> listMenuAccess = new List<UserMenuAccess>();
						List<UserMenuAccess> listMenuPermission = new List<UserMenuAccess>();

						Role role = _context.Using<Role>().GetByCondition(x => x.Id == obj.RoleId).FirstOrDefault();

						if (role == null)
						{
							CommonViewModel.IsSuccess = false;
							CommonViewModel.StatusCode = ResponseStatusCode.Error;
							CommonViewModel.Message = ResponseStatusMessage.Error;

							return Json(CommonViewModel);
						}
						else if (role != null && role.Id == 1)
						{
							listMenuAccess = (from y in _context.Using<Menu>().GetAll().ToList()
											  where y.IsActive == true && y.IsDeleted == false
											  select new UserMenuAccess() { Id = y.Id, ParentMenuId = y.ParentId, Area = y.Area, Controller = y.Controller, Url = y.Url, MenuName = y.Name, IsCreate = true, IsUpdate = true, IsRead = true, IsDelete = true, DisplayOrder = y.DisplayOrder, IsActive = y.IsActive, IsDeleted = y.IsDeleted }).ToList();
						}
						else if (role != null && (role.IsAdmin || role.Name.ToUpper() == "VENDOR"))
						{
							listMenuAccess = (from x in _context.Using<UserMenuAccess>().GetAll().ToList()
											  join y in _context.Using<Menu>().GetAll().ToList() on x.MenuId equals y.Id
											  where x.UserId == obj.Id && x.RoleId == obj.RoleId
											  && y.IsActive == true && y.IsDeleted == false && x.IsActive == true && x.IsDeleted == false && y.Name != "Menu"
											  && x.IsRead == true
											  select new UserMenuAccess() { Id = y.Id, ParentMenuId = y.ParentId, Area = y.Area, Controller = y.Controller, Url = y.Url, MenuName = y.Name, DisplayOrder = y.DisplayOrder, IsActive = x.IsActive, IsDeleted = x.IsDeleted }).ToList();
						}
						else if (role != null && !role.IsAdmin && role.IsActive && !role.IsDeleted)
						{
							listMenuAccess = (from x in _context.Using<UserMenuAccess>().GetAll().ToList()
											  join y in _context.Using<Menu>().GetAll().ToList() on x.MenuId equals y.Id
											  where x.UserId == obj.Id && x.RoleId == obj.RoleId
											  && y.IsActive == true && y.IsDeleted == false && x.IsActive == true && x.IsDeleted == false && y.Id != 1 && y.ParentId != 1 && y.Name != "Menu"
											  && x.IsRead == true
											  select new UserMenuAccess() { Id = y.Id, ParentMenuId = y.ParentId, Area = y.Area, Controller = y.Controller, Url = y.Url, MenuName = y.Name, DisplayOrder = y.DisplayOrder, IsActive = x.IsActive, IsDeleted = x.IsDeleted }).ToList();
						}

						if (role != null && role.Id == 1)
							listMenuPermission = listMenuAccess;
						else
							listMenuPermission = (from x in _context.Using<UserMenuAccess>().GetAll().ToList()
												  join y in _context.Using<Menu>().GetAll().ToList() on x.MenuId equals y.Id
												  where x.UserId == obj.Id && y.IsActive == true && y.IsDeleted == false && x.IsActive == true && x.IsDeleted == false
												  && listMenuAccess.Any(z => z.Id == y.Id)
												  select new UserMenuAccess() { MenuId = y.Id, ParentMenuId = y.ParentId, Area = y.Area, Controller = y.Controller, Url = y.Url, MenuName = y.Name, IsCreate = x.IsCreate, IsUpdate = x.IsUpdate, IsRead = x.IsRead, IsDelete = x.IsDelete, IsActive = x.IsActive, IsDeleted = x.IsDeleted }).ToList();

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
								var ProjectDetailTypeAccess = _context.Using<LOV>().GetByCondition(x => x.LOV_Column == "USER_TYPE" && x.LOV_Code == employee.UserType)
									.Select(x => x.ProjectDetailTypeAccess).FirstOrDefault();

								Common.Set_Session(SessionKey.ProjectDetailTypeAccess, ProjectDetailTypeAccess ?? "");
							}
						}

						Common.Set_Session_Int(SessionKey.KEY_VENDOR_ID, VendorId);

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
				CommonViewModel.IsSuccess = false;
				CommonViewModel.StatusCode = ResponseStatusCode.Error;
				CommonViewModel.Message = ResponseStatusMessage.Error + " | " + ex.Message;
			}

			return Json(CommonViewModel);
		}

		public ActionResult Logout()
		{
			Common.Clear_Session();

			return RedirectToAction("Account", "Home", new { Area = "Admin" });
		}

	}
}