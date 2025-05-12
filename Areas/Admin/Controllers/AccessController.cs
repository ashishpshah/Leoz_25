using Leoz_25.Controllers;
using Leoz_25.Infra;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Leoz_25.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class AccessController : BaseController<ResponseModel<UserMenuAccess>>
    {
        public AccessController(IRepositoryWrapper repository) : base(repository) { }

        //[CustomAuthorizeAttribute(AccessType_Enum.Read)]
        public ActionResult Index()
        {
            CommonViewModel.ObjList = new List<UserMenuAccess>();

            var listRole = _context.Using<Role>().GetByCondition(x => x.Id > 1).OrderBy(x => x.DisplayOrder).Select(x => new SelectListItem_Custom(x.Id.ToString(), x.Name, "R")).Distinct().ToList();

            if (CommonViewModel.SelectListItems == null) CommonViewModel.SelectListItems = new List<SelectListItem_Custom>();
            if (listRole != null && listRole.Count > 0) CommonViewModel.SelectListItems.AddRange(listRole);

            return View(CommonViewModel);
        }

        [HttpPost]
        [AllowAnonymous]
        public JsonResult GetSelectList_User(long RoleId = 0, bool IsActive = false)
        {
            if (RoleId == 0)
            {
                List<SelectListItem> list = (from x in _context.Using<UserRoleMapping>().GetAll().ToList()
                                             join y in _context.Using<User>().GetAll().ToList() on x.UserId equals y.Id
                                             where y.IsActive == IsActive && x.RoleId > 1
                                             select new SelectListItem { Value = x.Id.ToString(), Text = y.UserName }).Distinct().ToList();

                return Json(list);
            }
            else if (RoleId > 0)
            {
                List<SelectListItem> list = (from x in _context.Using<UserRoleMapping>().GetAll().ToList()
                                             join y in _context.Using<User>().GetAll().ToList() on x.UserId equals y.Id
                                             where x.RoleId == RoleId && y.IsActive == IsActive && x.RoleId > 1
                                             select new SelectListItem { Value = x.Id.ToString(), Text = y.UserName }).Distinct().ToList();

                return Json(list);
            }

            return Json(new List<SelectListItem>());
        }

        //[CustomAuthorizeAttribute(AccessType_Enum.Read)]
        public ActionResult Partial_AddEditForm(long RoleId = 0, long UserId = 0)
        {
            CommonViewModel.IsSuccess = true;

            CommonViewModel.Obj = new UserMenuAccess() { RoleId = RoleId, UserId = UserId };
            CommonViewModel.ObjList = new List<UserMenuAccess>();

            var loggedUser_UserId = Common.LoggedUser_Id();

            if (RoleId == 0)
            {
                CommonViewModel.IsSuccess = false;
                CommonViewModel.StatusCode = ResponseStatusCode.Error;
                CommonViewModel.Message = "Please select Role.";

                return PartialView("_Partial_AddEditForm", CommonViewModel);
            }

            List<Role> listRole = (from x in _context.Using<Role>().GetAll().ToList()
                                   join y in _context.Using<User>().GetAll().ToList() on UserId equals y.Id
                                   join z in _context.Using<UserRoleMapping>().GetAll().ToList() on y.Id equals z.UserId
                                   where x.Id == z.RoleId
                                   select new Role { Id = x.Id, Name = x.Name, IsAdmin = x.IsAdmin }).Distinct().ToList();

            if (listRole != null && listRole.Count() > 0 && RoleId == 0)
            {
                CommonViewModel.IsSuccess = false;
                CommonViewModel.StatusCode = ResponseStatusCode.Error;
                CommonViewModel.Message = "Please select Role.";

                return PartialView("_Partial_AddEditForm", CommonViewModel);
            }

            if (UserId == 0)
            {
                CommonViewModel.IsSuccess = false;
                CommonViewModel.StatusCode = ResponseStatusCode.Error;
                CommonViewModel.Message = "Please select User.";

                return PartialView("_Partial_AddEditForm", CommonViewModel);
            }

            if (UserId == loggedUser_UserId)
            {
                CommonViewModel.IsSuccess = false;
                CommonViewModel.StatusCode = ResponseStatusCode.Error;
                CommonViewModel.Message = "You do not change your own access. Please contact administrator.";

                return PartialView("_Partial_AddEditForm", CommonViewModel);
            }

            listRole = (from x in _context.Using<Role>().GetAll().ToList()
                        join y in _context.Using<User>().GetAll().ToList() on UserId equals y.Id
                        join z in _context.Using<UserRoleMapping>().GetAll().ToList() on y.Id equals z.UserId
                        where x.Id == RoleId
                        select new Role { Id = x.Id, Name = x.Name, IsAdmin = x.IsAdmin }).Distinct().ToList();

            List<Menu> listMenu = new List<Menu>();

            if (RoleId == 1 && listRole.Any(y => y.IsAdmin == true))
                listMenu = _context.Using<Menu>().GetAll().ToList();
            else if (RoleId != 1 && listRole.Any(y => y.IsAdmin == true))
                listMenu = _context.Using<Menu>().GetByCondition(x => x.IsSuperAdmin == false).ToList();
            else
                listMenu = _context.Using<Menu>().GetByCondition(x => x.IsSuperAdmin == false && x.IsAdmin == false).ToList();

            foreach (var item in listMenu.Where(x => x.ParentId > 0).ToList())
                item.ParentMenuName = listMenu.Where(x => x.Id == item.ParentId).Select(x => x.Name).FirstOrDefault();

            CommonViewModel.ObjList = (from x in listMenu.ToList()
                                       select new UserMenuAccess()
                                       {
                                           MenuId = x.Id,
                                           ParentMenuId = x.ParentId,
                                           Area = x.Area,
                                           Controller = x.Controller,
                                           Url = x.Url,
                                           MenuName = x.Name,
                                           ParentMenuName = x.ParentMenuName,
                                           DisplayOrder = x.DisplayOrder,
                                           IsActive = x.IsActive,
                                           IsDeleted = x.IsDeleted
                                       }).ToList();

            if (RoleId > 1 && UserId > 0)
            {
                foreach (var item in CommonViewModel.ObjList)
                {
                    if (_context.Using<UserMenuAccess>().GetAll().ToList().Any(x => x.UserId == UserId && x.RoleId == RoleId && x.MenuId == item.MenuId && x.RoleId > 1))
                    {
                        var access = _context.Using<UserMenuAccess>().GetByCondition(x => x.UserId == UserId && x.RoleId == RoleId && x.MenuId == item.MenuId && x.RoleId > 1).FirstOrDefault();

                        item.IsCreate = access.IsCreate;
                        item.IsUpdate = access.IsUpdate;
                        item.IsRead = access.IsRead;
                        item.IsDelete = access.IsDelete;
                        item.IsActive = access.IsActive;
                        item.IsDeleted = access.IsDeleted;
                    }
                }
            }

            List<Role> loggedUser_Roles = (from x in _context.Using<UserRoleMapping>().GetAll().ToList()
                                           join y in _context.Using<Role>().GetAll().ToList() on (x.RoleId, x.IsActive, x.IsDeleted) equals (y.Id, true, false)
                                           where x.UserId == loggedUser_UserId
                                           && y.IsActive == true && y.IsDeleted == false && x.IsActive == true && x.IsDeleted == false
                                           select y).ToList();

            if (loggedUser_Roles.Any(x => x.IsAdmin == true && x.Id != 1) && listRole.Any(x => x.Id != 1))
                ((List<UserMenuAccess>)CommonViewModel.ObjList).RemoveAll(x => x.MenuName == "Menu" || x.ParentMenuName == "Menu");

            if (listRole.Any(x => x.IsAdmin != true))
                ((List<UserMenuAccess>)CommonViewModel.ObjList).RemoveAll(x => x.MenuName == "Menu" || x.ParentMenuName == "Menu" || x.MenuId == 1 || x.ParentMenuId == 1);

            return PartialView("_Partial_AddEditForm", CommonViewModel);
        }

        [HttpPost]
        //[CustomAuthorizeAttribute(AccessType_Enum.Write)]
        public ActionResult Save(ResponseModel<UserMenuAccess> viewModel)
        {
            try
            {
                if (viewModel != null && viewModel.Obj != null)
                {
                    if (viewModel.Obj.RoleId == 0)
                    {
                        CommonViewModel.IsSuccess = false;
                        CommonViewModel.StatusCode = ResponseStatusCode.Error;
                        CommonViewModel.Message = "Please select Role.";

                        return Json(CommonViewModel);
                    }

                    if (viewModel.Obj.UserId == 0)
                    {
                        CommonViewModel.IsSuccess = false;
                        CommonViewModel.StatusCode = ResponseStatusCode.Error;
                        CommonViewModel.Message = "Please select User.";

                        return Json(CommonViewModel);
                    }


                    var loggedUser_UserId = Common.LoggedUser_Id();

                    List<Role> roles = (from x in _context.Using<UserRoleMapping>().GetAll().ToList()
                                        join y in _context.Using<Role>().GetAll().ToList() on (x.RoleId, x.IsActive, x.IsDeleted) equals (y.Id, true, false)
                                        where x.UserId == loggedUser_UserId
                                        && y.IsActive == true && y.IsDeleted == false && x.IsActive == true && x.IsDeleted == false
                                        select y).ToList();

                    if (roles == null || roles.Count == 0 || !roles.Any(x => x.IsAdmin == true))
                    {
                        CommonViewModel.IsSuccess = false;
                        CommonViewModel.StatusCode = ResponseStatusCode.Error;
                        CommonViewModel.Message = "You are not authorized to perform this action.";

                        return Json(CommonViewModel);
                    }

                    var listParentMenu = new List<UserMenuAccess>();

                    foreach (var parentMenuId in viewModel.ObjList.Where(x => x.ParentMenuId > 0).Select(x => x.ParentMenuId).Distinct().ToList())
                    {
                        var parentMenu = new UserMenuAccess()
                        {
                            MenuId = parentMenuId,
                            IsCreate = viewModel.ObjList.Any(x => x.ParentMenuId == parentMenuId && x.IsCreate == true),
                            IsUpdate = viewModel.ObjList.Any(x => x.ParentMenuId == parentMenuId && x.IsUpdate == true),
                            IsRead = viewModel.ObjList.Any(x => x.ParentMenuId == parentMenuId && x.IsRead == true),
                            IsDelete = viewModel.ObjList.Any(x => x.ParentMenuId == parentMenuId && x.IsDelete == true),
                        };
                        listParentMenu.Add(parentMenu);
                    }
                    if (listParentMenu != null && listParentMenu.Count > 0)
                        viewModel.ObjList.AddRange(listParentMenu);

                    List<UserMenuAccess> listUserMenuAccess = _context.Using<UserMenuAccess>().GetByCondition(x => x.UserId == viewModel.Obj.UserId && x.RoleId == viewModel.Obj.RoleId).ToList();

                    if (listUserMenuAccess != null && listUserMenuAccess.Count() > 0)
                    {
                        foreach (var access in listUserMenuAccess)
                        {
                            _context.Using<UserMenuAccess>().Delete(access);
                            //_context.Entry(access).State = EntityState.Deleted;
                            //_context.SaveChanges();
                        }
                    }

                    foreach (var menu in viewModel.ObjList.Where(x => !(x.IsCreate == false && x.IsUpdate == false && x.IsRead == false && x.IsDelete == false)).ToList())
                    {
                        try
                        {
                            //var obj = listUserMenuAccess.Where(x => x.MenuId == menu.MenuId).FirstOrDefault();

                            //if (obj != null)
                            //{
                            //	obj.UserId = viewModel.Obj.UserId;
                            //	obj.RoleId = viewModel.Obj.RoleId;

                            //	//obj.IsSetDefault = true;
                            //	obj.IsActive = true;
                            //	obj.IsCreate = menu.IsCreate;
                            //	obj.IsUpdate = menu.IsUpdate;
                            //	obj.IsRead = menu.IsRead;
                            //	obj.IsDelete = menu.IsDelete;

                            //	_context.Entry(obj).State = EntityState.Modified;
                            //	_context.SaveChanges();
                            //}
                            //else
                            //{
                            menu.UserId = viewModel.Obj.UserId;
                            menu.RoleId = viewModel.Obj.RoleId;

                            //menu.IsSetDefault = true;
                            menu.IsActive = true;

                            _context.Using<UserMenuAccess>().Add(menu);
                            //_context.SaveChanges();
                            //}
                        }
                        catch (Exception ex)
                        { continue; }
                    }


                    //foreach (var menu in listUserMenuAccess.ToList())
                    //{
                    //	try
                    //	{
                    //		var obj = viewModel.ObjList.ToList().Where(x => x.MenuId == menu.MenuId).FirstOrDefault();

                    //		if (obj == null)
                    //		{
                    //			obj = new UserMenuAccess();

                    //			obj.UserId = viewModel.Obj.UserId;
                    //			obj.RoleId = viewModel.Obj.RoleId;

                    //			//obj.IsSetDefault = true;
                    //			obj.IsActive = true;

                    //			obj.IsCreate = false;
                    //			obj.IsUpdate = false;
                    //			obj.IsRead = false;
                    //			obj.IsDelete = false;

                    //			_context.Using<UserMenuAccess>().Add(obj);
                    //			_context.SaveChanges();
                    //		}
                    //	}
                    //	catch (Exception ex)
                    //	{ continue; }
                    //}


                    CommonViewModel.IsConfirm = true;
                    CommonViewModel.IsSuccess = true;
                    CommonViewModel.StatusCode = ResponseStatusCode.Success;
                    CommonViewModel.Message = "Record saved successfully ! ";

                    CommonViewModel.RedirectURL = Url.Action("Index", "Access", new { area = "Admin" }) + "/Index";

                    return Json(CommonViewModel);
                }
            }
            catch (Exception ex)
            { }

            CommonViewModel.IsSuccess = false;
            CommonViewModel.StatusCode = ResponseStatusCode.Error;
            CommonViewModel.Message = ResponseStatusMessage.Error;

            return Json(CommonViewModel);
        }

    }
}