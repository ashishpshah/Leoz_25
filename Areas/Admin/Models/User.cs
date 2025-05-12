
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace Leoz_25
{
	public partial class User : EntitiesBase
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public override long Id { get; set; }

		[NotMapped] public string UserId { get; set; }
		public string UserName { get; set; }
		public string Password { get; set; }
		//public string EmailId { get; set; }
		//public string MobileNo { get; set; }
		//public long? RoleId { get; set; }

		public Nullable<int> No_Of_Wrong_Password_Attempts { get; set; }
		public Nullable<DateTime> Next_Change_Password_Date { get; set; }

		[NotMapped] public string User_Role { get; set; }
		[NotMapped] public long User_Role_Id { get; set; }
		[NotMapped] public long RoleId { get; set; }
		[NotMapped] public bool IsPassword_Reset { get; set; }
		[NotMapped] public DateTime? Date { get; set; }
		[NotMapped] public string Date_Text { get; set; }
		[NotMapped] public string User_Id_Str { get; set; }
		[NotMapped] public string Role_Id_Str { get; set; }


		//[NotMapped] public string Get_User_Id => this.Id > 0 ? Common.Encrypt(this.Id.ToString()) : null;
		//[NotMapped] public string Get_Role_Id => this.Id > 0 ? Common.Encrypt(this.RoleId.ToString()) : null;

		//[NotMapped] public long Decrypt_Id { get { return !string.IsNullOrEmpty(this.Id) ? Convert.ToInt64(Common.Decrypt(this.Id)) : 0; } }
		//[NotMapped] public long Decrypt_RoleId { get { return !string.IsNullOrEmpty(this.Id) ? Convert.ToInt64(Common.Decrypt(this.Id)) : 0; } }
	}
}