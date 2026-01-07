using Leoz_25.Controllers;
using Leoz_25.Infra;
using Microsoft.AspNetCore.Mvc;
using Mono.TextTemplating;
using System.Composition;
using System.Numerics;

namespace Leoz_25.Areas.Admin.Controllers
{
	[Area("Admin")]
	public class VendorSubscriptionController : BaseController<ResponseModel<VendorSubscription>>
	{
		public VendorSubscriptionController(IRepositoryWrapper repository) : base(repository) { }

		// GET: Admin/VendorSubscription
		public ActionResult Index()
		{
			CommonViewModel.Obj = _context.Using<VendorSubscription>().GetByCondition(x => x.VendorId == Logged_In_VendorId).OrderByDescending(x => x.EndDate?.Ticks).FirstOrDefault();

			var list = _context.Using<Package>().GetByCondition(x => x.IsActive == true || x.Id == (CommonViewModel.Obj != null ? CommonViewModel.Obj.PackageId : -1)).ToList();

			if (CommonViewModel.Obj != null) CommonViewModel.Obj.Selected_Package = list.Where(x => x.Id == CommonViewModel.Obj.PackageId).FirstOrDefault();

			if (CommonViewModel.Obj != null != null)
			{
				CommonViewModel.Obj.StartDate_Text = CommonViewModel.Obj.StartDate != DateTime.MinValue ? CommonViewModel.Obj.StartDate.ToString("dd/MM/yyyy").Replace("-", "/") : "";
				CommonViewModel.Obj.EndDate_Text = CommonViewModel.Obj.EndDate != DateTime.MinValue ? CommonViewModel.Obj.EndDate?.ToString("dd/MM/yyyy").Replace("-", "/") : "";
			}

			CommonViewModel.Data1 = list;

			return View(CommonViewModel);
		}

		[HttpPost]
		//[CustomAuthorizeAttribute(AccessType_Enum.Write)]
		public ActionResult Subscribe(long PackageId = 0)
		{
			try
			{
				if (PackageId > 0)
				{
					#region Validation

					if (!_context.Using<Package>().Any(x => x.Id == PackageId) || _context.Using<Package>().Any(x => x.Id == PackageId && x.IsActive == false))
					{
						CommonViewModel.IsSuccess = false;
						CommonViewModel.StatusCode = ResponseStatusCode.Error;
						CommonViewModel.Message = "Selected Plan is not available to subscribe now.";

						return Json(CommonViewModel);
					}

					var subscription = _context.Using<VendorSubscription>().GetByCondition(x => x.VendorId == Logged_In_VendorId).OrderByDescending(x => x.EndDate?.Ticks).FirstOrDefault();

					var selected_Package = _context.Using<Package>().GetByCondition(x => x.Id == (subscription != null ? subscription.PackageId : -1)).FirstOrDefault();


					if (subscription != null && selected_Package.IsYearly == true && subscription.StartDate.Date.Ticks <= DateTime.Now.Date.Ticks && subscription.EndDate?.Date.Ticks >= DateTime.Now.Date.Ticks && (subscription.IsActive == false || subscription.IsCancelled == true))
					{
						CommonViewModel.IsSuccess = false;
						CommonViewModel.StatusCode = ResponseStatusCode.Error;
						CommonViewModel.Message = (subscription.IsCancelled == true ? "Subscribe plan is Cancelled. " : "You are already subscribe a plan. but plan not active. ") + "Please contact administration.";

						return Json(CommonViewModel);
					}
					else if (subscription != null && selected_Package.IsYearly == true && subscription.StartDate.Date.Ticks <= DateTime.Now.Date.Ticks && subscription.EndDate?.Date.Ticks >= DateTime.Now.Date.Ticks)
					{
						CommonViewModel.IsSuccess = false;
						CommonViewModel.StatusCode = ResponseStatusCode.Error;
						CommonViewModel.Message = "You are already subscribe a plan.";

						return Json(CommonViewModel);
					}
					else if (subscription != null && selected_Package.IsYearly == true && subscription.StartDate.Date.Ticks > DateTime.Now.Date.Ticks)
					{
						CommonViewModel.IsSuccess = false;
						CommonViewModel.StatusCode = ResponseStatusCode.Error;
						CommonViewModel.Message = $"You are already subscribe a plan and started at {subscription.StartDate.Date.ToString(Common.DateTimeFormat_ddMMyyyy)}.";

						return Json(CommonViewModel);
					}

					var listCustomer = _context.Using<Customer>().GetByCondition(x => Logged_In_VendorId > 0 ? x.VendorId == Logged_In_VendorId : false).ToList();

					if (selected_Package != null && selected_Package.IsProjectBased == true
						&& selected_Package.ProjectLimit > (listCustomer != null ? listCustomer.Count() : 0))
					{
						CommonViewModel.IsSuccess = false;
						CommonViewModel.StatusCode = ResponseStatusCode.Error;
						CommonViewModel.Message = $"You are already used plan Project Limit.";

						return Json(CommonViewModel);
					}

					var listEmployee = _context.Using<Employee>().GetByCondition(x => Logged_In_VendorId > 0 ? x.VendorId == Logged_In_VendorId : false).ToList();

					if (listEmployee == null || listEmployee.Count() == 0)
					{
						CommonViewModel.IsSuccess = false;
						CommonViewModel.StatusCode = ResponseStatusCode.Error;
						CommonViewModel.Message = $"There are no employees associated with this vendor. Please First Add Employee of your company";

						return Json(CommonViewModel);
					}


					#endregion

					#region Database-Transaction

					selected_Package = _context.Using<Package>().GetByCondition(x => x.Id == PackageId).FirstOrDefault();

					VendorSubscription obj = new VendorSubscription()
					{
						VendorId = Logged_In_VendorId,
						PackageId = PackageId,
						StartDate = selected_Package.IsYearly == true ? DateTime.Now.Date : DateTime.MinValue,
						EndDate = selected_Package.IsYearly == true ? DateTime.Now.AddDays(selected_Package.DurationInDays - 1).Date : DateTime.MinValue
					};

					if (obj != null)
					{
						_context.Using<VendorSubscription>().Add(obj);
						//_context.SaveChanges();
					}

					CommonViewModel.IsConfirm = true;
					CommonViewModel.IsSuccess = true;
					CommonViewModel.StatusCode = ResponseStatusCode.Success;
					CommonViewModel.Message = ResponseStatusMessage.Success;

					CommonViewModel.RedirectURL = Url.Action("Index", "VendorSubscription", new { area = "Admin" });

					return Json(CommonViewModel);

					#endregion
				}
			}
			catch (Exception ex) { LogService.LogInsert(GetCurrentAction(), "", ex); }

			CommonViewModel.Message = ResponseStatusMessage.Error;
			CommonViewModel.IsSuccess = false;
			CommonViewModel.StatusCode = ResponseStatusCode.Error;

			return Json(CommonViewModel);
		}


	}

}