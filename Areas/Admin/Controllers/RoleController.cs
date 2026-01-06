using Leoz_25.Controllers;
using Leoz_25.Infra;
using Microsoft.AspNetCore.Mvc;
using System.Linq;

namespace Leoz_25.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class RoleController : BaseController<ResponseModel<Role>>
    {
        public RoleController(IRepositoryWrapper repository) : base(repository) { }

        // GET: Admin/Role
        public ActionResult Index()
        {
            if (Common.IsSuperAdmin())
                CommonViewModel.ObjList = (from x in _context.Using<Role>().GetAll().ToList()
                                           where x.Id > 1
                                           select new Role { Id = x.Id, Name = x.Name, IsActive = x.IsActive, IsAdmin = x.IsAdmin }).Distinct().ToList();

            else if (!Common.IsSuperAdmin() && Common.IsAdmin())
                CommonViewModel.ObjList = (from x in _context.Using<Role>().GetAll().ToList()
                                           where x.Id > 1
                                           select new Role { Id = x.Id, Name = x.Name, IsActive = x.IsActive, IsAdmin = x.IsAdmin }).Distinct().ToList();

            return View(CommonViewModel);
        }

        //[CustomAuthorizeAttribute(AccessType_Enum.Read)]
        public ActionResult Partial_AddEditForm(long Id = 0)
        {
            CommonViewModel.Obj = new Role();

            if (Common.IsAdmin() && Id > 1)
                CommonViewModel.Obj = _context.Using<Role>().GetByCondition(x => x.Id > 1 && x.Id == Id).FirstOrDefault();


            var listMenu = _context.Using<Menu>().GetAll().ToList();

            foreach (var item in listMenu.Where(x => x.ParentId > 0).ToList())
                item.ParentMenuName = listMenu.Where(x => x.Id == item.ParentId).Select(x => x.Name).FirstOrDefault();

            CommonViewModel.SelectListItems = new List<SelectListItem_Custom>();

            var listUOM = _context.Using<LOV>().GetByCondition(x => x.LOV_Column == "ProjectDetailType").ToList();

            if (listUOM != null && listUOM.Count() > 0)
                CommonViewModel.SelectListItems.AddRange(listUOM.Select(x => new SelectListItem_Custom(x.LOV_Code, x.LOV_Desc, "PD", x.DisplayOrder)).ToList());

            var menus = new List<SelectListItem_Custom>();

            if (Common.IsSuperAdmin())
            {
                menus = (from x in listMenu.ToList()
                         where !x.Name.ToLower().Contains("menu") //x.IsSuperAdmin == Common.IsSuperAdmin() && x.IsAdmin == Common.IsAdmin()
                         orderby x.ParentId, x.DisplayOrder
                         select new SelectListItem_Custom(Convert.ToString(x.Id + "_" + x.ParentId), Convert.ToString(x.Name) + (x.ParentId > 0 ? " (" + Convert.ToString(x.ParentMenuName) + " )" : ""), "M")).ToList();
            }
            else
            {
                menus = (from x in listMenu.ToList()
                         where x.IsSuperAdmin == false && !x.Name.ToLower().Contains("menu")
                         orderby x.ParentId, x.DisplayOrder
                         select new SelectListItem_Custom(Convert.ToString(x.Id + "_" + x.ParentId), Convert.ToString(x.Name) + (x.ParentId > 0 ? " (" + Convert.ToString(x.ParentMenuName) + " )" : ""), "M")).ToList();
            }

            if (menus != null && menus.Count > 0) CommonViewModel.SelectListItems.AddRange(menus);

            var list = _context.Using<RoleMenuAccess>().GetByCondition(x => x.RoleId == CommonViewModel.Obj.Id).ToList();

            if (list != null && list.Count() > 0)
            {
                string[] selected = (from x in list
                                     join y in listMenu on x.MenuId equals y.Id
                                     where !y.Name.ToLower().Contains("menu") && !y.Name.ToLower().Contains("menu")
                                     select Convert.ToString(x.MenuId + "_" + y.ParentId)).ToArray();

                if (selected != null && selected.Length > 0)
                    CommonViewModel.Obj.CreatedDate_Text = string.Join(",", selected) + ",";
            }

            if (CommonViewModel.SelectListItems == null) CommonViewModel.SelectListItems = new List<SelectListItem_Custom>();

            return PartialView("_Partial_AddEditForm", CommonViewModel);
        }

        [HttpPost]
        //[CustomAuthorizeAttribute(AccessType_Enum.Write)]
        public ActionResult Save(Role viewModel)
        {
            try
            {
                viewModel = viewModel != null && !Common.IsSuperAdmin() && viewModel.Id == Common.LoggedUser_RoleId() ? null : viewModel;

                if (viewModel != null)
                {
                    #region Validation

                    if (!Common.IsAdmin())
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
                        CommonViewModel.Message = "Please enter Role name.";

                        return Json(CommonViewModel);
                    }

                    if (_context.Using<Role>().Any(x => x.Name.ToLower().Replace(" ", "") == viewModel.Name.ToLower().Replace(" ", "") && x.Id != viewModel.Id) || viewModel.Id == 1)
                    {
                        CommonViewModel.IsSuccess = false;
                        CommonViewModel.StatusCode = ResponseStatusCode.Error;
                        CommonViewModel.Message = "Role already exist. Please try another Role.";

                        return Json(CommonViewModel);
                    }

                    #endregion

                    #region Database-Transaction

                    using (var transaction = _context.BeginTransaction())
                    {
                        try
                        {
                            Role obj = _context.Using<Role>().GetByCondition(x => x.Id > 1 && x.Id == viewModel.Id).FirstOrDefault();

                            if (viewModel != null && !(viewModel.DisplayOrder > 0))
                                viewModel.DisplayOrder = (_context.Using<Role>().GetAll().ToList().Max(x => x.DisplayOrder) ?? 0) + 1;

                            if (Common.IsAdmin() && obj != null)
                            {
                                obj.Name = viewModel.Name;
                                obj.DisplayOrder = viewModel.DisplayOrder;
                                obj.IsAdmin = Common.IsSuperAdmin() ? viewModel.IsAdmin : false;
                                obj.IsActive = viewModel.IsActive;
                                obj.ProjectDetailTypeAccess = viewModel.ProjectDetailTypeAccess;

                                _context.Using<Role>().Update(obj);
                                //_context.Entry(obj).State = EntityState.Modified;
                                //_context.SaveChanges();
                            }
                            else if (Common.IsAdmin())
                            {
                                viewModel.IsAdmin = Common.IsSuperAdmin() ? viewModel.IsAdmin : false;

                                var _viewModel = _context.Using<Role>().Add(viewModel);
                                viewModel.Id = _viewModel.Id;
                                //_context.SaveChanges();
                                //_context.Entry(viewModel).Reload();
                            }

                            try
                            {
                                var listRoleMenuAccesses = _context.Using<RoleMenuAccess>().GetByCondition(x => x.RoleId == viewModel.Id).ToList();

                                if (listRoleMenuAccesses != null && listRoleMenuAccesses.Count() > 0)
                                {
                                    foreach (var access in listRoleMenuAccesses)
                                    {
                                        _context.Using<RoleMenuAccess>().Delete(access);
                                        //_context.Entry(access).State = EntityState.Deleted;
                                        //_context.SaveChanges();
                                    }
                                }

                                if (!string.IsNullOrEmpty(viewModel.CreatedDate_Text))
                                {
                                    var listMenu = _context.Using<Menu>().GetAll().ToList();

                                    List<(long MenuId, long ParentMenuId)> menuPairs = viewModel.CreatedDate_Text.Split(',').Where(x => !string.IsNullOrEmpty(x)).Select(pair => pair.Split('_'))
                                                    .Select(parts => (MenuId: long.Parse(parts[0]), ParentMenuId: long.Parse(parts[1]))).ToList();

                                    // Get all unique IDs from MenuId and ParentMenuId
                                    var allMenuIds = menuPairs.SelectMany(p => new[] { p.MenuId, p.ParentMenuId }).Distinct().ToList();

                                    // Now get actual Menu objects by matching IDs
                                    var selectedMenus = listMenu.Where(x => x.IsActive == true && allMenuIds.Contains(x.Id)).ToList();

                                    //var list = viewModel.CreatedDate_Text.Split(',');

                                    //foreach (var item in list.Where(x => !string.IsNullOrEmpty(x)))
                                    foreach (var menu in selectedMenus)
                                    {
                                        try
                                        {
                                            var roleMenuAccess = new RoleMenuAccess()
                                            {
                                                //MenuId = Convert.ToInt64(item.Split('_')[0]),
                                                MenuId = menu.Id,
                                                RoleId = viewModel.Id,
                                                IsCreate = true,
                                                IsUpdate = true,
                                                IsRead = true,
                                                IsDelete = true,
                                                IsActive = true,
                                                IsDeleted = false,
                                                IsSetDefault = true
                                            };

                                            _context.Using<RoleMenuAccess>().Add(roleMenuAccess);
                                            //_context.SaveChanges();
                                        }
                                        catch (Exception ex) { continue; }
                                    }


                                    try
                                    {
                                        var listUserMenuAccesses = _context.Using<UserMenuAccess>().GetByCondition(x => x.RoleId == viewModel.Id).ToList();

                                        foreach (var access in listUserMenuAccesses) _context.Using<UserMenuAccess>().Delete(access);
                                        //_context.Entry(access).State = EntityState.Deleted;
                                        //_context.SaveChanges();

                                        foreach (var menu in selectedMenus.OrderBy(x => x.Id).ToList())
                                        {
                                            var listRoleUserAccess = (from x in _context.Using<UserRoleMapping>().GetAll().ToList()
                                                                      where x.RoleId == viewModel.Id && x.UserId > 0 && x.IsActive == true && x.IsDeleted == false
                                                                      select new UserMenuAccess() { UserId = x.UserId, RoleId = viewModel.Id, MenuId = menu.Id, IsCreate = true, IsUpdate = true, IsRead = true, IsDelete = true, CreatedBy = 1 }).ToList();

                                            foreach (var item_ in listRoleUserAccess) _context.Using<UserMenuAccess>().Add(item_);
                                            //_context.SaveChanges();
                                        }
									}
									catch (Exception ex) { LogService.LogInsert(GetCurrentAction(), "", ex); }

								}

							}
							catch (Exception ex) { LogService.LogInsert(GetCurrentAction(), "", ex); }

							CommonViewModel.IsConfirm = true;
                            CommonViewModel.IsSuccess = true;
                            CommonViewModel.StatusCode = ResponseStatusCode.Success;
                            CommonViewModel.Message = ResponseStatusMessage.Success;
                            CommonViewModel.RedirectURL = Url.Action("Index", "Role", new { area = "Admin" });

                            transaction.Commit();

                            return Json(CommonViewModel);
						}
						catch (Exception ex) { LogService.LogInsert(GetCurrentAction(), "", ex); transaction.Rollback(); }
                    }

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
                if (!Common.IsSuperAdmin() || !Common.IsAdmin())
                {
                    CommonViewModel.IsSuccess = false;
                    CommonViewModel.StatusCode = ResponseStatusCode.Error;
                    CommonViewModel.Message = ResponseStatusMessage.UnAuthorize;

                    return Json(CommonViewModel);
                }

                if (Common.IsSuperAdmin() && Common.IsAdmin() && !_context.Using<UserRoleMapping>().Any(x => x.Id > 1 && x.RoleId == Id)
                    && _context.Using<Role>().Any(x => x.Id > 1 && x.Id == Id))
                {
                    var obj = _context.Using<Role>().GetByCondition(x => x.Id == Id).FirstOrDefault();

                    _context.Using<Role>().Update(obj);
                    //_context.Entry(obj).State = EntityState.Deleted;
                    //_context.SaveChanges();

                    CommonViewModel.IsConfirm = true;
                    CommonViewModel.IsSuccess = true;
                    CommonViewModel.StatusCode = ResponseStatusCode.Success;
                    CommonViewModel.Message = ResponseStatusMessage.Delete;

                    CommonViewModel.RedirectURL = Url.Action("Index", "Role", new { area = "Admin" });

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