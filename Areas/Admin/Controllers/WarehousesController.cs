using LEOZ.Controllers;
using LEOZ.Infra;
using LEOZ.Infra.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Globalization;

namespace LEOZ.Areas.Admin.Controllers
{
	[Area("Admin")]
	public class WarehousesController : BaseController<ResponseModel<Warehouses>>
	{
		public WarehousesController(IRepositoryWrapper repository) : base(repository) { }

		// GET: Admin/Warehouses
		public ActionResult Index()
		{
			CommonViewModel.ObjList = DataContext_Command.Warehouses_Get(0).ToList();

			return View(CommonViewModel);
		}

		//public ActionResult Users()
		//{
		//	//CommonViewModel.ObjList = DataContext_Command.Warehouses_Get(0).ToList();

		//	return View(CommonViewModel);
		//}

		//[CustomAuthorizeAttribute(AccessType_Enum.Read)]
		public ActionResult Partial_AddEditForm(long Id = 0)
		{
			CommonViewModel.Obj = new Warehouses() { };

			if (Id > 0) CommonViewModel.Obj = DataContext_Command.Warehouses_Get(Id).FirstOrDefault();
            var list = new List<SelectListItem_Custom>();

            var parameters = new List<SqlParameter>();

            if (CommonViewModel.Obj != null && CommonViewModel.Obj.Id > 0)
			{
				var obj = DataContext_Command.Warehouses_Get(Id);


                
				parameters = new List<SqlParameter>();
                parameters.Add(new SqlParameter("@Id", 0));
				parameters.Add(new SqlParameter("@State_Id", CommonViewModel.Obj.StateId));
                var dt1 = new DataTable();
                dt1 = DataContext_Command.ExecuteStoredProcedure_DataTable("SP_City_Get", parameters);
                if (dt1 != null && dt1.Rows.Count > 0) { foreach (DataRow dr in dt1.Rows) list.Add(new SelectListItem_Custom(Convert.ToString(dr["Id"]), Convert.ToString(dr["Name"]), "City")); }
                   }

		
			parameters.Add(new SqlParameter("@Id", 0));
			 var dt = new DataTable();
            dt = DataContext_Command.ExecuteStoredProcedure_DataTable("SP_State_Get", parameters);
            if (dt != null && dt.Rows.Count > 0) { foreach (DataRow dr in dt.Rows) list.Add(new SelectListItem_Custom(Convert.ToString(dr["Id"]), Convert.ToString(dr["State_Name"]), "State")); }


            CommonViewModel.SelectListItems = list;
            return PartialView("_Partial_AddEditForm", CommonViewModel);
		}
      
            public IActionResult GetCity(long ParentId = 0)
		   { 
				var list = new List<SelectListItem_Custom>();
			   List<SqlParameter> oParams = new List<SqlParameter>(); var dt = new DataTable(); 
			   try
			   { 
				 oParams = new List<SqlParameter>();
				 oParams.Add(new SqlParameter("@Id", SqlDbType.BigInt) { Value = 0 }); 
				 oParams.Add(new SqlParameter("@State_Id", SqlDbType.BigInt) { Value = ParentId }); 
				 dt = DataContext_Command.ExecuteStoredProcedure_DataTable("SP_City_Get", oParams, true); 
				 if (dt != null && dt.Rows.Count > 0) 
					foreach (DataRow dr in dt.Rows) list.Add(new SelectListItem_Custom(Convert.ToString(dr["Id"]), Convert.ToString(dr["Name"]), "City"));
			   }
			 catch (Exception ex)
			 {  
				LogService.LogInsert(GetCurrentAction(), "", ex);
			 } 
			    return Json(list); 
		   }

        [HttpPost]
		//[CustomAuthorizeAttribute(AccessType_Enum.Write)]
		public ActionResult Save(Warehouses viewModel)
		{
			try
			{
				if (viewModel != null && viewModel != null)
				{
					#region Validation

					if (string.IsNullOrEmpty(viewModel.WarehouseName))
					{
						CommonViewModel.IsSuccess = false;
						CommonViewModel.StatusCode = ResponseStatusCode.Error;
						CommonViewModel.Message = "Please enter WarehouseName.";

						return Json(CommonViewModel);
					}

					if (string.IsNullOrEmpty(viewModel.ContactPerson))
					{
						CommonViewModel.IsSuccess = false;
						CommonViewModel.StatusCode = ResponseStatusCode.Error;
						CommonViewModel.Message = "Please enter ContactPerson.";

						return Json(CommonViewModel);
					}

					if (!string.IsNullOrEmpty(viewModel.Email) && !ValidateField.IsValidEmail(viewModel.Email))
					{
						CommonViewModel.IsSuccess = false;
						CommonViewModel.StatusCode = ResponseStatusCode.Error;
						CommonViewModel.Message = "Please enter valid Email.";

						return Json(CommonViewModel);
					}

					if (!string.IsNullOrEmpty(viewModel.Phone) && !ValidateField.IsValidMobileNo_D10(viewModel.Phone))
					{
						CommonViewModel.IsSuccess = false;
						CommonViewModel.StatusCode = ResponseStatusCode.Error;
						CommonViewModel.Message = "Please enter valid Phone No.";

						return Json(CommonViewModel);
					}

					//if (!string.IsNullOrEmpty(viewModel.ContactNo_Alternate) && !ValidateField.IsValidMobileNo(viewModel.ContactNo_Alternate))
					//{
					//	CommonViewModel.IsSuccess = false;
					//	CommonViewModel.StatusCode = ResponseStatusCode.Error;
					//	CommonViewModel.Message = "Please enter valid Alternate Contact No.";

					//	return Json(CommonViewModel);
					//}

					#endregion

					#region Database-Transaction

					try
					{
						//if (viewModel.IsPassword_Reset == true)
						//	viewModel.Password = Common.Encrypt("12345");

						var (IsSuccess, response, Id) = DataContext_Command.Warehouses_Save(viewModel);
						viewModel.Id = Id;

						CommonViewModel.IsConfirm = IsSuccess;
						CommonViewModel.IsSuccess = IsSuccess;
						CommonViewModel.StatusCode = IsSuccess ? ResponseStatusCode.Success : ResponseStatusCode.Error;
						CommonViewModel.Message = response;
						CommonViewModel.RedirectURL = Url.Action("Index", "Warehouses", new { area = "Admin" });

						return Json(CommonViewModel);
					}
					catch (Exception ex) { }

					#endregion
				}
			}
			catch (Exception ex) { }

			CommonViewModel.Message = ResponseStatusMessage.Error;
			CommonViewModel.IsSuccess = false;
			CommonViewModel.StatusCode = ResponseStatusCode.Error;

			return Json(CommonViewModel);
		}

		[HttpPost]
		[CustomAuthorizeAttribute(AccessType_Enum.Delete)]
		public ActionResult DeleteConfirmed(long Id)
		{
			try
			{
				//var (IsSuccess, response) = DataContext_Command.Warehouses_Status(Id, false, true);

				//CommonViewModel.IsConfirm = IsSuccess;
				//CommonViewModel.IsSuccess = IsSuccess;
				//CommonViewModel.StatusCode = IsSuccess ? ResponseStatusCode.Success : ResponseStatusCode.Error;
				//CommonViewModel.Message = response;
				//CommonViewModel.RedirectURL = Url.Action("Index", "Warehouses", new { area = "Admin" });

				//return Json(CommonViewModel);
			}
			catch (Exception ex) { }

			CommonViewModel.IsSuccess = false;
			CommonViewModel.StatusCode = ResponseStatusCode.Error;
			CommonViewModel.Message = ResponseStatusMessage.Unable_Delete;

			return Json(CommonViewModel);
		}

	}

}