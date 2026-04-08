
using Leoz_25.Controllers;
using Leoz_25.Infra;
using Microsoft.AspNetCore.Mvc;

namespace Leoz_25.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class ChangePasswordController : BaseController<ResponseModel<ChangePassword>>
    {
        public ChangePasswordController(IRepositoryWrapper repository) : base(repository) { }

        public IActionResult Index()
        {
            CommonViewModel.Obj = new ChangePassword();
            return View(CommonViewModel);
        }
        [HttpPost]
        public IActionResult Save(ResponseModel<ChangePassword> viewModel)
        
        {
            try
            {
                if (viewModel?.Obj == null)
                {
                    CommonViewModel.Message = "Invalid request";
                    CommonViewModel.IsSuccess = false;
                    CommonViewModel.StatusCode = ResponseStatusCode.Error;
                    return Json(CommonViewModel);
                }
                if (string.IsNullOrEmpty(viewModel.Obj.OldPassword))
                {
                    CommonViewModel.Message = "Please Enter Old Password";
                    CommonViewModel.IsSuccess = false;
                    CommonViewModel.StatusCode = ResponseStatusCode.Error;
                    return Json(CommonViewModel);
                }

                using (var transaction = _context.BeginTransaction())
                {
                    try
                    {
                        var userId = Common.LoggedUser_Id();
                        var user = _context.Using<User>().GetByCondition(x => x.Id == userId).FirstOrDefault();

                        if (user == null)
                        {
                            CommonViewModel.Message = "User not found!";
                            CommonViewModel.IsSuccess = false;
                            CommonViewModel.StatusCode = ResponseStatusCode.Error;
                            return Json(CommonViewModel);
                        }
                        var enteredOldPasswordEncrypted = Common.Encrypt(viewModel.Obj.OldPassword);
                        // user.Password = Common.Decrypt(viewModel.Obj.OldPassword);
                        if (user.Password != enteredOldPasswordEncrypted)
                        {
                            CommonViewModel.Message = "Old password is incorrect!";
                            CommonViewModel.IsSuccess = false;
                            CommonViewModel.StatusCode = ResponseStatusCode.Error;
                            return Json(CommonViewModel);
                        }
                        if (string.IsNullOrEmpty(viewModel.Obj.NewPassword))
                        {
                            CommonViewModel.Message = "Please Enter New Password";
                            CommonViewModel.IsSuccess = false;
                            CommonViewModel.StatusCode = ResponseStatusCode.Error;
                            return Json(CommonViewModel);
                        }
                        if (string.IsNullOrEmpty(viewModel.Obj.ConfirmPassword))
                        {
                            CommonViewModel.Message = "Please Enter Confirm Password";
                            CommonViewModel.IsSuccess = false;
                            CommonViewModel.StatusCode = ResponseStatusCode.Error;
                            return Json(CommonViewModel);
                        }
                        if (viewModel.Obj.NewPassword != viewModel.Obj.ConfirmPassword)
                        {
                            CommonViewModel.Message = "Confirm password does not match new password!";
                            CommonViewModel.IsSuccess = false;
                            CommonViewModel.StatusCode = ResponseStatusCode.Error;
                            return Json(CommonViewModel);
                        }

                        var encryptedNewPassword = Common.Encrypt(viewModel.Obj.NewPassword);
                        // Update password
                        user.Password = encryptedNewPassword;
                        user.LastModifiedDate = DateTime.Now;
                        user.LastModifiedBy = userId;

                        _context.Using<User>().Update(user);
                        CommonViewModel.IsConfirm = true;
                        CommonViewModel.IsSuccess = true;
                        CommonViewModel.Message = "Password changed successfully!";
                        CommonViewModel.StatusCode = ResponseStatusCode.Success;
                        CommonViewModel.RedirectURL = Url.Action("Account", "Home", new { area = "Admin" });
                        transaction.Commit();
                        return Json(CommonViewModel);
                    }
                    catch (Exception ex) { transaction.Rollback(); }
                }
            }
            catch (Exception ex)
            {


            }
            CommonViewModel.Message = ResponseStatusMessage.Error;
            CommonViewModel.IsSuccess = false;
            CommonViewModel.StatusCode = ResponseStatusCode.Error;
            return Json(CommonViewModel);
        }

    }
}
