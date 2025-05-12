using Leoz_25.Infra;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.Blazor;

namespace Leoz_25
{
    public interface IResponseModel
    {
        string ActionMode { get; set; }
        bool IsCreate { get; set; }
        bool IsDelete { get; set; }
        bool IsRead { get; set; }
        bool IsUpdate { get; set; }

        List<SelectListItem_Custom> SelectListItems { get; set; }
    }

    public class ResponseModel<TModel> : BaseModel where TModel : class
    {
        public TModel Obj { get; set; }

        public List<TModel> ObjList { get; set; }
        public string ActionMode { get; set; }

        public bool IsCreate { get; set; }
        public bool IsUpdate { get; set; }
        public bool IsRead { get; set; }
        public bool IsDelete { get; set; }

        public List<SelectListItem_Custom> SelectListItems { get; set; }

        public ResponseModel()
        {
            IsCreate = true;
            IsUpdate = true;
            IsRead = true;
            IsDelete = true;

            Status = "error";
            Message = ResponseStatusMessage.Error;
            IsSuccess = false;
            IsConfirm = false;
            StatusCode = ResponseStatusCode.Error;
        }

    }

    public class BaseModel
    {
        public string UserId { get; set; }
        public string Action { get; set; }
        public string Message { get; set; }
        public int StatusCode { get; set; }
        public string Status { get; set; }
        public string RedirectURL { get; set; }
        public bool IsSuccess { get; set; }
        public bool IsConfirm { get; set; }

        public dynamic Data1 { get; set; }
        public dynamic Data2 { get; set; }
        public dynamic Data3 { get; set; }
        public dynamic Data4 { get; set; }
        public dynamic Data5 { get; set; }
        // public List<SelectListItem> SelectList1 { get; set; }

    }

    public class ApiResponseModel
    {
        public int StatusCode { get; set; }
        public string Message { get; set; }
        public bool IsSuccess { get; set; }

        public dynamic Data1 { get; set; }
        public dynamic Data2 { get; set; }
        public dynamic Data3 { get; set; }
        public dynamic Data4 { get; set; }
        public dynamic Data5 { get; set; }


        public ApiResponseModel()
        {
            Message = ResponseStatusMessage.Error;
            IsSuccess = false;
            StatusCode = ResponseStatusCode.Error;

            Data1 = null;
            Data2 = null;
            Data3 = null;
            Data4 = null;
            Data5 = null;
        }
    }
}