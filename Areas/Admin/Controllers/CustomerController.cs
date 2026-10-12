using Leoz_25.Controllers;
using Leoz_25.Infra;
using Leoz_25.Infra.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Globalization;
using System.Linq;

namespace Leoz_25.Areas.Admin.Controllers
{
	[Area("Admin")]
	public class CustomerController : BaseController<ResponseModel<Customer>>
	{
		public CustomerController(IRepositoryWrapper repository) : base(repository) { }

		// GET: Admin/Customer
		public ActionResult Index()
		{
			CommonViewModel.ObjList = _context.Using<Customer>().GetByCondition(x => x.VendorId == Logged_In_VendorId).ToList();

			var listProject = (from x in _context.Using<CustomerProjectMapping>().GetByCondition(x => x.VendorId == Logged_In_VendorId).Distinct().ToList()
							   join z in _context.Using<Project>().GetByCondition(x => x.VendorId == Logged_In_VendorId).ToList() on x.ProjectId equals z.Id
							   select new { CustomerId = x.CustomerId, ProjectName = z.Name }).ToList();

			if (listProject != null && listProject.Count > 0 && CommonViewModel.ObjList != null && CommonViewModel.ObjList.Count() > 0)
				foreach (var item in CommonViewModel.ObjList)
					item.Projects = string.Join(", ", listProject.Where(x => x.CustomerId == item.Id).Select(x => x.ProjectName).ToArray());

			if (CommonViewModel.ObjList != null && CommonViewModel.ObjList.Count() > 0)
			{
				var listInUseCustomerId = Get_InUse_CustomerIds(CommonViewModel.ObjList.ToList());

				foreach (var item in CommonViewModel.ObjList) item.IsInUse = listInUseCustomerId.Contains(item.Id);
			}

			return View(CommonViewModel);
		}

		/// <summary>
		/// Edit Customer : protect projects which are already started.
		/// 1. Project(s) mapping : Project where work is already started can not be removed from Customer.
		/// 2. Customer mapped to an active Project can not be made In-Active.
		/// Returns error message, or null when valid.
		/// </summary>
		private string Get_ProjectMapping_Error(Customer viewModel, bool IsProjectMap)
		{
			if (viewModel == null || viewModel.Id <= 0) return null;

			var objCustomer = _context.Using<Customer>().GetByCondition(x => x.Id == viewModel.Id && x.VendorId == Logged_In_VendorId).FirstOrDefault();

			if (objCustomer == null) return "Customer not found.";

			var dictProject = _context.Using<Project>().GetByCondition(x => x.VendorId == Logged_In_VendorId).ToDictionary(x => x.Id, x => x);

			string ProjectNames(IEnumerable<long> ids) => string.Join(", ", ids.Distinct().Select(x => dictProject.TryGetValue(x, out var p) ? p.Name : x.ToString()));

			var listOldProjectId = _context.Using<CustomerProjectMapping>().GetByCondition(x => x.CustomerId == objCustomer.Id && x.VendorId == Logged_In_VendorId).Select(x => x.ProjectId).Distinct().ToList();

			if (IsProjectMap)
			{
				var listNewProjectId = (viewModel.ProjectIds ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)
					.Select(x => long.TryParse(x.Trim(), out var id) ? id : 0).Where(x => x > 0).Distinct().ToList();

				// Project is mapped to only one Customer (documents / pending work of project are shown to mapped Customer)
				var listOtherCustomerProjectId = _context.Using<CustomerProjectMapping>().GetByCondition(x => listNewProjectId.Contains(x.ProjectId) && x.CustomerId != objCustomer.Id && x.VendorId == Logged_In_VendorId).Select(x => x.ProjectId).Distinct().ToList();

				if (listOtherCustomerProjectId.Count > 0)
					return $"Project(s) : {ProjectNames(listOtherCustomerProjectId)} already mapped to another Customer.";

				var listRemovedProjectId = listOldProjectId.Where(x => !listNewProjectId.Contains(x)).ToList();

				if (listRemovedProjectId.Count > 0)
				{
					// Work started in Project (Project Detail / Daily Update need Customer mapping to open project)
					var listStartedProjectId = new List<long>();

					listStartedProjectId.AddRange(_context.Using<ProjectSiteDoc>().GetByCondition(x => listRemovedProjectId.Contains(x.ProjectId)).Select(x => x.ProjectId));
					listStartedProjectId.AddRange(_context.Using<ProjectSiteDoc_History>().GetByCondition(x => listRemovedProjectId.Contains(x.ProjectId)).Select(x => x.ProjectId));
					listStartedProjectId.AddRange(_context.Using<ProjectSiteMaterial>().GetByCondition(x => listRemovedProjectId.Contains(x.ProjectId)).Select(x => x.ProjectId));
					listStartedProjectId.AddRange(_context.Using<ProjectSitePendingWork>().GetByCondition(x => listRemovedProjectId.Contains(x.ProjectId)).Select(x => x.ProjectId));
					listStartedProjectId.AddRange(_context.Using<AgencyMaster>().GetByCondition(x => listRemovedProjectId.Contains(x.ProjectId)).Select(x => x.ProjectId));
					listStartedProjectId.AddRange(_context.Using<ProjectDailyUpdate>().GetByCondition(x => listRemovedProjectId.Contains(x.ProjectId)).Select(x => x.ProjectId));

					if (listStartedProjectId.Count > 0)
						return $"Work is already started in Project(s) : {ProjectNames(listStartedProjectId)}. Can not remove these Project(s) from Customer.";
				}
			}
			else
			{
				var listActiveMappedProjectId = listOldProjectId.Where(x => dictProject.TryGetValue(x, out var p) && p.IsActive == true).ToList();

				if (objCustomer.IsActive == true && viewModel.IsActive == false && listActiveMappedProjectId.Count > 0)
					return $"Customer is mapped to active Project(s) : {ProjectNames(listActiveMappedProjectId)}. Please make Project(s) In-Active / remove Project(s) first, then make Customer In-Active.";
			}

			return null;
		}

		/// <summary>
		/// Customers who are mapped to a project, or project work is started for them
		/// (Documents, Document history, Material, Pending Work, Daily Update),
		/// or Customer login has already worked (uploaded / approved) in project.
		/// </summary>
		private HashSet<long> Get_InUse_CustomerIds(List<Customer> listCustomer)
		{
			var result = new HashSet<long>();

			if (listCustomer == null || listCustomer.Count == 0) return result;

			var listCustomerId = listCustomer.Select(x => x.Id).ToList();

			result.UnionWith(_context.Using<CustomerProjectMapping>().GetByCondition(x => listCustomerId.Contains(x.CustomerId)).Select(x => x.CustomerId));
			result.UnionWith(_context.Using<ProjectSiteDoc>().GetByCondition(x => listCustomerId.Contains(x.CustomerId)).Select(x => x.CustomerId));
			result.UnionWith(_context.Using<ProjectSiteDoc_History>().GetByCondition(x => listCustomerId.Contains(x.CustomerId)).Select(x => x.CustomerId));
			result.UnionWith(_context.Using<ProjectSiteMaterial>().GetByCondition(x => listCustomerId.Contains(x.CustomerId)).Select(x => x.CustomerId));
			result.UnionWith(_context.Using<ProjectSitePendingWork>().GetByCondition(x => listCustomerId.Contains(x.CustomerId)).Select(x => x.CustomerId));
			result.UnionWith(_context.Using<ProjectDailyUpdate>().GetByCondition(x => listCustomerId.Contains(x.CustomerId)).Select(x => x.CustomerId));

			// Work done by Customer login is tracked by User Id (CreatedBy / LastModifiedBy)
			var dictUserCustomer = listCustomer.Where(x => x.UserId > 0).GroupBy(x => x.UserId).ToDictionary(x => x.Key, x => x.Select(y => y.Id).ToList());
			var listUserId = dictUserCustomer.Keys.ToList();

			if (listUserId.Count > 0)
			{
				var listWorkedUserId = new List<long>();

				listWorkedUserId.AddRange(_context.Using<ProjectSiteDoc>().GetByCondition(x => listUserId.Contains(x.CreatedBy) || listUserId.Contains(x.LastModifiedBy)).SelectMany(x => new[] { x.CreatedBy, x.LastModifiedBy }));
				listWorkedUserId.AddRange(_context.Using<ProjectSitePendingWork>().GetByCondition(x => listUserId.Contains(x.CreatedBy) || listUserId.Contains(x.LastModifiedBy)).SelectMany(x => new[] { x.CreatedBy, x.LastModifiedBy }));
				listWorkedUserId.AddRange(_context.Using<AgencyMaster>().GetByCondition(x => listUserId.Contains(x.CreatedBy) || listUserId.Contains(x.LastModifiedBy)).SelectMany(x => new[] { x.CreatedBy, x.LastModifiedBy }));

				foreach (var userId in listWorkedUserId.Distinct())
					if (dictUserCustomer.TryGetValue(userId, out var listId)) result.UnionWith(listId);
			}

			return result;
		}

		//[CustomAuthorizeAttribute(AccessType_Enum.Read)]
		public ActionResult Partial_AddEditForm(long Id = 0, bool IsMapProject = false)
		{
			CommonViewModel.Obj = new Customer() { };

			if (Id > 0) CommonViewModel.Obj = _context.Using<Customer>().GetByCondition(x => x.Id == Id && x.VendorId == Logged_In_VendorId).FirstOrDefault();

			var listProjectId = new List<long>();

			if (CommonViewModel.Obj != null && CommonViewModel.Obj.UserId > 0)
			{
				var obj = _context.Using<User>().GetByCondition(x => x.Id == CommonViewModel.Obj.UserId).FirstOrDefault();

				if (obj != null && obj.IsActive == true && obj.IsDeleted == false)
					CommonViewModel.Obj.UserName = obj.UserName;

				listProjectId = (from x in _context.Using<CustomerProjectMapping>().GetByCondition(x => x.CustomerId == CommonViewModel.Obj.Id && x.VendorId == Logged_In_VendorId).Distinct().ToList()
								 join z in _context.Using<Project>().GetByCondition(x => x.VendorId == Logged_In_VendorId).ToList() on x.ProjectId equals z.Id
								 select z.Id).ToList();

				if (listProjectId != null && listProjectId.Count > 0)
					CommonViewModel.Obj.ProjectIds = string.Join(",", listProjectId.Select(x => x).ToArray());

			}

			if (IsMapProject == true)
			{
				CommonViewModel.SelectListItems = new List<SelectListItem_Custom>();

				var listProject = _context.Using<Project>().GetByCondition(x => (x.IsActive == true || listProjectId.Contains(x.Id))
										&& x.VendorId == Logged_In_VendorId).Distinct().ToList();

				if (listProject != null && listProject.Count > 0)
					CommonViewModel.SelectListItems.AddRange(listProject.Select(x => new SelectListItem_Custom(x.Id.ToString(), x.Name)).ToList());
			}

			CommonViewModel.Data1 = IsMapProject;

			return PartialView("_Partial_AddEditForm", CommonViewModel);
		}

		[HttpPost]
		//[CustomAuthorizeAttribute(AccessType_Enum.Write)]
		public ActionResult Save(Customer viewModel)
		{
			try
			{
				var subscriptionError = Get_Subscription_Error();

				if (!string.IsNullOrEmpty(subscriptionError))
				{
					CommonViewModel.IsSuccess = false;
					CommonViewModel.StatusCode = ResponseStatusCode.Error;
					CommonViewModel.Message = subscriptionError;

					return Json(CommonViewModel);
				}

				//var selected_Package = _context.Using<Package>().GetByCondition(x => x.Id == (subscription != null ? subscription.PackageId : -1)).FirstOrDefault();

				//if (selected_Package == null)
				//{
				//	CommonViewModel.IsSuccess = false;
				//	CommonViewModel.StatusCode = ResponseStatusCode.Error;
				//	CommonViewModel.Message = "You are already subscribe a plan. but plan not active. Please contact administration.";

				//	return Json(CommonViewModel);
				//}

				//if (subscription != null && selected_Package.IsYearly == true && subscription.StartDate.Date.Ticks <= DateTime.Now.Date.Ticks && subscription.EndDate.Date.Ticks >= DateTime.Now.Date.Ticks && (subscription.IsActive == false || subscription.IsCancelled == true))
				//{
				//	CommonViewModel.IsSuccess = false;
				//	CommonViewModel.StatusCode = ResponseStatusCode.Error;
				//	CommonViewModel.Message = (subscription.IsCancelled == true ? "Subscribe plan is Cancelled. " : "You are already subscribe a plan. but plan not active. ") + "Please contact administration.";

				//	return Json(CommonViewModel);
				//}
				//else if (subscription != null && selected_Package.IsYearly == true && subscription.StartDate.Date.Ticks <= DateTime.Now.Date.Ticks && subscription.EndDate.Date.Ticks >= DateTime.Now.Date.Ticks)
				//{
				//	CommonViewModel.IsSuccess = false;
				//	CommonViewModel.StatusCode = ResponseStatusCode.Error;
				//	CommonViewModel.Message = "You are already subscribe a plan.";

				//	return Json(CommonViewModel);
				//}
				//else if (subscription != null && selected_Package.IsYearly == true && subscription.StartDate.Date.Ticks > DateTime.Now.Date.Ticks)
				//{
				//	CommonViewModel.IsSuccess = false;
				//	CommonViewModel.StatusCode = ResponseStatusCode.Error;
				//	CommonViewModel.Message = $"You are already subscribe a plan and started at {subscription.StartDate.Date.ToString(Common.DateTimeFormat_ddMMyyyy)}.";

				//	return Json(CommonViewModel);
				//}

				//var listCustomer = _context.Using<Customer>().GetByCondition(x => x.VendorId == Logged_In_VendorId && x.IsActive == true).ToList();

				//if (selected_Package != null && selected_Package.IsProjectBased == true
				//	&& ((selected_Package.ProjectLimit > 0 && !(selected_Package.ProjectLimit > (listCustomer != null ? listCustomer.Count() : 0))) || selected_Package.ProjectLimit <= 0))
				//{
				//	CommonViewModel.IsSuccess = false;
				//	CommonViewModel.StatusCode = ResponseStatusCode.Error;
				//	CommonViewModel.Message = $"You are already used plan Project Limit.";

				//	return Json(CommonViewModel);
				//}


				if (viewModel != null && viewModel != null && IsVendor)
				{
					if (!Request.Form.ContainsKey("ProjectMap"))
					{
						#region Validation

						if (string.IsNullOrEmpty(viewModel.UserName))
						{
							CommonViewModel.IsSuccess = false;
							CommonViewModel.StatusCode = ResponseStatusCode.Error;
							CommonViewModel.Message = "Please enter User Name.";

							return Json(CommonViewModel);
						}

						if (viewModel.Id == 0 && string.IsNullOrEmpty(viewModel.Password))
						{
							CommonViewModel.IsSuccess = false;
							CommonViewModel.StatusCode = ResponseStatusCode.Error;
							CommonViewModel.Message = "Please enter Password.";

							return Json(CommonViewModel);
						}

						if (string.IsNullOrEmpty(viewModel.FirstName))
						{
							CommonViewModel.IsSuccess = false;
							CommonViewModel.StatusCode = ResponseStatusCode.Error;
							CommonViewModel.Message = "Please enter First Name.";

							return Json(CommonViewModel);
						}
						//if (string.IsNullOrEmpty(viewModel.MiddleName))
						//{
						//	CommonViewModel.IsSuccess = false;
						//	CommonViewModel.StatusCode = ResponseStatusCode.Error;
						//	CommonViewModel.Message = "Please enter Middle Name.";

						//	return Json(CommonViewModel);
						//}

						if (string.IsNullOrEmpty(viewModel.LastName))
						{
							CommonViewModel.IsSuccess = false;
							CommonViewModel.StatusCode = ResponseStatusCode.Error;
							CommonViewModel.Message = "Please enter Last Name.";

							return Json(CommonViewModel);
						}

						if (!string.IsNullOrEmpty(viewModel.Email) && !ValidateField.IsValidEmail(viewModel.Email))
						{
							CommonViewModel.IsSuccess = false;
							CommonViewModel.StatusCode = ResponseStatusCode.Error;
							CommonViewModel.Message = "Please enter valid Email.";

							return Json(CommonViewModel);
						}

						if (!string.IsNullOrEmpty(viewModel.ContactNo) && !ValidateField.IsValidMobileNo(viewModel.ContactNo))
						{
							CommonViewModel.IsSuccess = false;
							CommonViewModel.StatusCode = ResponseStatusCode.Error;
							CommonViewModel.Message = "Please enter valid Contact No.";

							return Json(CommonViewModel);
						}
                        if (!string.IsNullOrEmpty(viewModel.Contact_PersonNo) && !ValidateField.IsValidMobileNo(viewModel.Contact_PersonNo))
                        {
                            CommonViewModel.IsSuccess = false;
                            CommonViewModel.StatusCode = ResponseStatusCode.Error;
                            CommonViewModel.Message = "Please enter valid Contact Person No.";

                            return Json(CommonViewModel);
                        }
                        if (/*Logged_In_VendorId <= 0 && */_context.Using<User>().Any(x => x.UserName.ToLower() == viewModel.UserName.ToLower() && x.Id != viewModel.UserId))
						{
							CommonViewModel.Message = "User Name already exist. Please try another User Name.";
							CommonViewModel.IsSuccess = false;
							CommonViewModel.StatusCode = ResponseStatusCode.Error;

							return Json(CommonViewModel);
						}

                        if (!string.IsNullOrWhiteSpace(viewModel.Contact_PersonNo) && !System.Text.RegularExpressions.Regex.IsMatch(viewModel.Contact_PersonNo.Trim(), @"^\d{10}$"))
                        {
                            CommonViewModel.IsSuccess = false;
                            CommonViewModel.StatusCode = ResponseStatusCode.Error;
                            CommonViewModel.Message = "Contact Person No must be exactly 10 digits.";

                            return Json(CommonViewModel);
                        }
                        //if (Logged_In_VendorId > 0 && _context.Using<User>().GetByCondition(x => x.UserName.ToLower() == viewModel.UserName.ToLower() && x.Id != viewModel.UserId).ToList()
                        //	.Any(x => _context.Using<UserVendorMapping>().Any(z => z.VendorId == Logged_In_VendorId && z.UserId == x.Id) ))
                        //{
                        //	CommonViewModel.Message = "Username already exist. Please try another Username.";
                        //	CommonViewModel.IsSuccess = false;
                        //	CommonViewModel.StatusCode = ResponseStatusCode.Error;

                        //	return Json(CommonViewModel);
                        //}

                        if (_context.Using<Customer>().GetByCondition(x => x.VendorId == Logged_In_VendorId && x.Id != viewModel.Id).Any(x => x.Fullname.ToLower() == viewModel.Fullname.ToLower()))
						{
							CommonViewModel.Message = "Customer already exist. Please try another Customer name(s).";
							CommonViewModel.IsSuccess = false;
							CommonViewModel.StatusCode = ResponseStatusCode.Error;

							return Json(CommonViewModel);
						}

						#endregion
					}

					var projectError = Get_ProjectMapping_Error(viewModel, IsProjectMap: Request.Form.ContainsKey("ProjectMap") && Convert.ToBoolean(Request.Form["ProjectMap"]) == true);

					if (!string.IsNullOrEmpty(projectError))
					{
						CommonViewModel.IsSuccess = false;
						CommonViewModel.StatusCode = ResponseStatusCode.Error;
						CommonViewModel.Message = projectError;

						return Json(CommonViewModel);
					}

					#region Database-Transaction

					try
					{
						using (var transaction = _context.BeginTransaction())
						{
							try
							{
								if (!Request.Form.ContainsKey("ProjectMap"))
								{
									if (viewModel.IsPassword_Reset == true) viewModel.Password = "12345";

									if (!string.IsNullOrEmpty(viewModel.Password)) viewModel.Password = Common.Encrypt(viewModel.Password);

									viewModel.RoleId = _context.Using<Role>().GetByCondition(x => x.Name.ToLower() == "customer").Select(x => x.Id).FirstOrDefault();

									User obj = _context.Using<User>().GetByCondition(x => x.Id == viewModel.UserId).FirstOrDefault();

									if (obj != null)
									{
										obj.UserName = viewModel.UserName;

										if (viewModel.IsPassword_Reset == true) obj.Password = viewModel.Password;

										obj.IsActive = viewModel.IsActive;

										_context.Using<User>().Update(obj);
									}
									else
									{
										obj = new User();

										obj.UserName = viewModel.UserName;
										obj.Password = viewModel.Password;
										obj.IsActive = viewModel.IsActive;
										
										var _obj = _context.Using<User>().Add(obj);
										viewModel.UserId = _obj.Id;
									}

									Customer customer = _context.Using<Customer>().GetByCondition(x => x.Id == viewModel.Id).FirstOrDefault();

									if (customer != null)
									{
										customer.UserId = viewModel.UserId;
										customer.VendorId = Logged_In_VendorId;
										customer.FirstName = viewModel.FirstName;
										customer.MiddleName = viewModel.MiddleName;
										customer.LastName = viewModel.LastName;
										customer.Email = viewModel.Email;
										customer.ContactNo = viewModel.ContactNo;
										customer.Contact_PersonName = viewModel.Contact_PersonName;
										customer.Contact_PersonNo = viewModel.Contact_PersonNo;
										customer.IsActive = viewModel.IsActive;

										_context.Using<Customer>().Update(customer);
									}
									else
									{
										viewModel.VendorId = Logged_In_VendorId;
										var _obj = _context.Using<Customer>().Add(viewModel);
										viewModel.Id = _obj.Id;
									}

									if (viewModel.Id > 0 && viewModel.UserId > 0 && viewModel.RoleId > 0)
									{
										try
										{
											UserRoleMapping UserRole = _context.Using<UserRoleMapping>().GetByCondition(x => x.UserId == viewModel.UserId && x.RoleId == viewModel.RoleId).FirstOrDefault();

											if (UserRole != null)
											{
												UserRole.RoleId = viewModel.RoleId;
												_context.Using<UserRoleMapping>().Update(UserRole);
											}
											else _context.Using<UserRoleMapping>().Add(new UserRoleMapping() { UserId = viewModel.UserId, RoleId = viewModel.RoleId });

											var listUserMenuAccess = _context.Using<UserMenuAccess>().GetByCondition(x => x.UserId == viewModel.UserId && x.RoleId == viewModel.RoleId).ToList();

											if (listUserMenuAccess != null && listUserMenuAccess.Count() > 0)
												foreach (var access in listUserMenuAccess) _context.Using<UserMenuAccess>().Delete(access);

											foreach (var item in _context.Using<RoleMenuAccess>().GetByCondition(x => x.RoleId == viewModel.RoleId).ToList())
											{
												var userMenuAccess = new UserMenuAccess()
												{
													MenuId = item.MenuId,
													UserId = viewModel.UserId,
													RoleId = viewModel.RoleId,
													IsCreate = item.IsCreate,
													IsUpdate = item.IsUpdate,
													IsRead = item.IsRead,
													IsDelete = item.IsDelete,
													IsActive = item.IsActive,
													IsDeleted = item.IsDelete,
													IsSetDefault = true
												};

												_context.Using<UserMenuAccess>().Add(userMenuAccess);
											}

										}
										catch (Exception ex) { LogService.LogInsert(GetCurrentAction(), "", ex); }
									}
								}
								else if (Request.Form.ContainsKey("ProjectMap") && Convert.ToBoolean(Request.Form["ProjectMap"]) == true)
								{
									Customer customer = _context.Using<Customer>().GetByCondition(x => x.Id == viewModel.Id && x.VendorId == Logged_In_VendorId).FirstOrDefault();

									if (customer != null)
									{
										var listCustomerProjectMapping = _context.Using<CustomerProjectMapping>().GetByCondition(x => x.CustomerId == viewModel.Id && x.VendorId == Logged_In_VendorId).ToList();

										if (listCustomerProjectMapping != null && listCustomerProjectMapping.Count() > 0)
											foreach (var access in listCustomerProjectMapping) _context.Using<CustomerProjectMapping>().Delete(access);

										// Map only Vendor's own projects
										var listVendorProjectId = _context.Using<Project>().GetByCondition(x => x.VendorId == Logged_In_VendorId).Select(x => x.Id.ToString()).ToList();

										if (!string.IsNullOrEmpty(viewModel.ProjectIds))
											foreach (var item in viewModel.ProjectIds.Split(',').Select(x => x.Trim()).Where(x => listVendorProjectId.Contains(x)).Distinct())
											{
												var cp = new CustomerProjectMapping()
												{
													ProjectId = Convert.ToInt64(item),
													CustomerId = viewModel.Id,
													VendorId = Logged_In_VendorId
												};

												_context.Using<CustomerProjectMapping>().Add(cp);
											}

									}
								}

								CommonViewModel.IsConfirm = true;
								CommonViewModel.IsSuccess = true;
								CommonViewModel.StatusCode = ResponseStatusCode.Success;
								CommonViewModel.Message = "Record saved successfully ! ";
								CommonViewModel.RedirectURL = Url.Action("Index", "Customer", new { area = "Admin" });

								transaction.Commit();

								return Json(CommonViewModel);
							}
							catch (Exception ex) { LogService.LogInsert(GetCurrentAction(), "", ex); transaction.Rollback(); }
						}

					}
					catch (Exception ex) { LogService.LogInsert(GetCurrentAction(), "", ex); }

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
				var objCustomer = IsVendor ? _context.Using<Customer>().GetByCondition(x => x.Id == Id && x.VendorId == Logged_In_VendorId).FirstOrDefault() : null;

				if (objCustomer != null && Get_InUse_CustomerIds(new List<Customer>() { objCustomer }).Contains(objCustomer.Id))
				{
					CommonViewModel.IsSuccess = false;
					CommonViewModel.StatusCode = ResponseStatusCode.Error;
					CommonViewModel.Message = "Customer is mapped to Project or work is already started. Can not delete.";

					return Json(CommonViewModel);
				}

				if (objCustomer != null)
				using (var transaction = _context.BeginTransaction())
				{
					var user = objCustomer.UserId > 0 ? _context.Using<User>().GetByCondition(x => x.Id == objCustomer.UserId).FirstOrDefault() : null;

					if (user != null)
					{
						var UserRole = _context.Using<UserRoleMapping>().GetByCondition(x => x.UserId == objCustomer.UserId).ToList();

						if (UserRole != null) foreach (var obj in UserRole) _context.Using<UserRoleMapping>().Delete(obj);

						var UserMenu = _context.Using<UserMenuAccess>().GetByCondition(x => x.UserId == objCustomer.UserId).ToList();

						if (UserMenu != null) foreach (var obj in UserMenu) _context.Using<UserMenuAccess>().Delete(obj);

						_context.Using<User>().Delete(user);

					}

					var listCustomerProjectMapping = _context.Using<CustomerProjectMapping>().GetByCondition(x => x.CustomerId == Id && x.VendorId == Logged_In_VendorId).ToList();

					if (listCustomerProjectMapping != null && listCustomerProjectMapping.Count() > 0)
						foreach (var access in listCustomerProjectMapping) _context.Using<CustomerProjectMapping>().Delete(access);

					_context.Using<Customer>().Delete(objCustomer);

					transaction.Commit();

					CommonViewModel.IsConfirm = true;
					CommonViewModel.IsSuccess = true;
					CommonViewModel.StatusCode = ResponseStatusCode.Success;
					CommonViewModel.Message = "Data deleted successfully ! ";

					CommonViewModel.RedirectURL = Url.Action("Index", "Customer", new { area = "Admin" });

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