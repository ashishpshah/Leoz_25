using System.Data;
using System.Security.Cryptography;
using System.Text;

namespace Leoz_25.Infra
{
	public class Common
	{
		private static string EncrKey = AppHttpContextAccessor.EncrKey;


		public static string DateTimeFormat_ddMMyyyy => "dd/MM/yyyy";
		public static string DateTimeFormat_ddMMyyyy_HHmm => "dd/MM/yyyy HH:mm";
		public static string DateTimeFormat_ddMMyyyy_hhmm => "dd/MM/yyyy hh:mm tt";
		public static string DateTimeFormat_ddMMyyyy_HHmmss => "dd/MM/yyyy HH:mm:ss";
		public static string DateTimeFormat_ddMMyyyy_hhmmss => "dd/MM/yyyy hh:mm:ss tt";
		public static string DateTimeFormat_HHmm => "HH:mm";
		public static string DateTimeFormat_HHmmss => "HH:mm:ss";
		public static string DateTimeFormat_hhmm => "hh:mm tt";
		public static string DateTimeFormat_hhmmss => "hh:mm:ss tt";

		public static void Clear_Session() => AppHttpContextAccessor.AppHttpContext.Session.Clear();

		public static void Set_Session_Int(string key, Int64 value) => AppHttpContextAccessor.AppHttpContext.Session.SetString(key, value.ToString());
		public static long Get_Session_Int(string key) => (AppHttpContextAccessor.AppHttpContext != null && AppHttpContextAccessor.AppHttpContext.Session.Keys.Any(x => x == key) ? Convert.ToInt64(AppHttpContextAccessor.AppHttpContext.Session.GetString(key) ?? "0") : -1);

		public static void Set_Session(string key, string value) => AppHttpContextAccessor.AppHttpContext.Session.SetString(key, value);
		public static string Get_Session(string key) => (AppHttpContextAccessor.AppHttpContext.Session.Keys.Any(x => x == key) ? AppHttpContextAccessor.AppHttpContext.Session.GetString(key) : null);



		private static string controller_action;
		public static void Set_Controller_Action(string _value) => controller_action = _value;
		public static string Get_Controller_Action => controller_action;


		public static bool IsUserLogged() => Get_Session_Int(SessionKey.KEY_USER_ID) > 0;
		public static bool IsSuperAdmin() => Get_Session_Int(SessionKey.KEY_USER_ROLE_ID) == 1;
		public static bool IsAdmin() => Get_Session_Int(SessionKey.KEY_IS_ADMIN) == 1;
		public static Int64 LoggedUser_Id() => Get_Session_Int(SessionKey.KEY_USER_ID);
		public static Int64 LoggedUser_RoleId() => Get_Session_Int(SessionKey.KEY_USER_ROLE_ID);


		private static List<UserMenuAccess> UserMenuAccess;
		private static List<UserMenuAccess> UserMenuPermission;

		public static void Configure_UserMenuAccess(List<UserMenuAccess> userMenuAccess, List<UserMenuAccess> userMenuPermission)
		{
			UserMenuAccess = userMenuAccess;
			UserMenuPermission = userMenuPermission;
		}


		public static List<UserMenuAccess> GetUserMenuAccesses() => UserMenuAccess;
		public static List<UserMenuAccess> GetUserMenuPermission() => UserMenuPermission;


		public static bool CheckValueIsNull(object val) => !(val != DBNull.Value && val != null && !string.IsNullOrEmpty(Convert.ToString(val)));

		public static string Encrypt(string strText)
		{
			try
			{
				if (!string.IsNullOrEmpty(strText))
				{
					byte[] byKey = { };
					byte[] IV = {
							0x12,
							0x34,
							0x56,
							0x78,
							0x90,
							0xab,
							0xcd,
							0xef
						};

					//byKey = System.Text.Encoding.UTF8.GetBytes(Strings.Left(strEncrKey, 8));
					byKey = System.Text.Encoding.UTF8.GetBytes(EncrKey.Substring(0, 8));
					//byKey = System.Text.Encoding.UTF8.GetBytes(Strings.Left(strEncrKey, 8));
					DESCryptoServiceProvider des = new DESCryptoServiceProvider();
					byte[] inputByteArray = Encoding.UTF8.GetBytes(strText);
					MemoryStream ms = new MemoryStream();
					CryptoStream cs = new CryptoStream(ms, des.CreateEncryptor(byKey, IV), CryptoStreamMode.Write);
					cs.Write(inputByteArray, 0, inputByteArray.Length);
					cs.FlushFinalBlock();
					return Convert.ToBase64String(ms.ToArray());

				}
			}
			catch (ExecutionEngineException ex) { }

			return "";
		}

		public static string Decrypt(string strText)
		{
			byte[] byKey = { };
			byte[] IV = {
							0x12,
							0x34,
							0x56,
							0x78,
							0x90,
							0xab,
							0xcd,
							0xef
						};
			byte[] inputByteArray = new byte[strText.Length + 1];
			try
			{
				//byKey = System.Text.Encoding.UTF8.GetBytes(Strings.Left(sDecrKey, 8));
				byKey = System.Text.Encoding.UTF8.GetBytes(EncrKey.Substring(0, 8));
				using (System.Security.Cryptography.DESCryptoServiceProvider des = new System.Security.Cryptography.DESCryptoServiceProvider())
				{
					inputByteArray = Convert.FromBase64String(strText);
					using (MemoryStream ms = new MemoryStream())
					{
						using (CryptoStream cs = new CryptoStream(ms, des.CreateDecryptor(byKey, IV), CryptoStreamMode.Write))
						{
							cs.Write(inputByteArray, 0, inputByteArray.Length);
							cs.FlushFinalBlock();
							System.Text.Encoding encoding = System.Text.Encoding.UTF8;
							return encoding.GetString(ms.ToArray());
						}
					}
				}

			}
			catch (ExecutionEngineException ex)
			{
				return ex.Message;
			}
		}


		//public static async Task<(bool IsSuccess, string Message)> SendEmail(string subject, string body, string[] to_mails, List<(Stream contentStream, string contentType, string? fileDownloadName)> attachments, bool isBodyHtml = false)
		//{
		//    //LogService.LogInsert("Common - SendEmail", $"Send Email => Starting at {DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss").Replace("-", "/")} | Subject : {subject} | To : {string.Join(", ", to_mails)}", null);

		//    (bool IsSuccess, string Message) result = (false, "Sending Mail service is stop.");

		//    try
		//    {
		//        if (AppHttpContextAccessor.IsSendMail)
		//        {
		//            if (to_mails == null || !AppHttpContextAccessor.IsSendMail_Vendor)
		//                to_mails = new string[] { };

		//            if (to_mails == null || to_mails.Length == 0)
		//                to_mails = AppHttpContextAccessor.AdminToMail.Replace(" ", "").Replace(";", ",").Split(",").ToArray();

		//            //LogService.LogInsert("Common - SendEmail", $"Send Email => Update To mail address at {DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss").Replace("-", "/")} | Subject : {subject} | To : {string.Join(", ", to_mails)}", null);

		//            using (System.Net.Mail.MailMessage mailMessage = new System.Net.Mail.MailMessage())
		//            {
		//                mailMessage.From = new System.Net.Mail.MailAddress(AppHttpContextAccessor.AdminFromMail, AppHttpContextAccessor.DisplayName);

		//                mailMessage.Subject = subject;

		//                mailMessage.Body = body;

		//                mailMessage.IsBodyHtml = isBodyHtml;

		//                foreach (string item in to_mails)
		//                    mailMessage.To.Add(new System.Net.Mail.MailAddress(item.Trim())); // Trim to remove leading/trailing spaces

		//                //if (to_mails.Contains(","))
		//                //{
		//                //	foreach (string item in toEmail.Split(','))
		//                //	{
		//                //		mailMessage.To.Add(new System.Net.Mail.MailAddress(item.Trim())); // Trim to remove leading/trailing spaces
		//                //	}
		//                //}
		//                //else if (toEmail.Contains(";"))
		//                //{
		//                //	//foreach (string item in toEmail.Split(';'))
		//                //	//{
		//                //	//	mailMessage.To.Add(new System.Net.Mail.MailAddress(item.Trim())); // Trim to remove leading/trailing spaces
		//                //	//}
		//                //}
		//                //else
		//                //{
		//                //	mailMessage.To.Add(new System.Net.Mail.MailAddress(toEmail));
		//                //}

		//                // Create and configure SMTP client
		//                //System.Net.Mail.SmtpClient smtp = new System.Net.Mail.SmtpClient();
		//                //smtp.Host = AppHttpContextAccessor.Host;
		//                //smtp.Port = int.Parse(AppHttpContextAccessor.Port);
		//                //smtp.EnableSsl = Convert.ToBoolean(AppHttpContextAccessor.EnableSsl);

		//                //// Provide authentication credentials
		//                //smtp.UseDefaultCredentials = false; // Set to false to specify custom credentials
		//                //smtp.Credentials = new System.Net.NetworkCredential(AppHttpContextAccessor.AdminFromMail, AppHttpContextAccessor.MailPassword);

		//                // Configure the SMTP client
		//                System.Net.Mail.SmtpClient smtpClient = new System.Net.Mail.SmtpClient(AppHttpContextAccessor.Host, AppHttpContextAccessor.Port)
		//                {
		//                    EnableSsl = AppHttpContextAccessor.EnableSsl,  // Enable SSL (Implicit TLS)
		//                    Credentials = new System.Net.NetworkCredential(AppHttpContextAccessor.AdminFromMail, AppHttpContextAccessor.MailPassword),
		//                    DeliveryMethod = System.Net.Mail.SmtpDeliveryMethod.Network,
		//                    UseDefaultCredentials = AppHttpContextAccessor.DefaultCredentials
		//                };

		//                // Send the email
		//                await smtpClient.SendMailAsync(mailMessage);

		//                result.IsSuccess = true;
		//                result.Message = "Mail Sent successfully.";
		//            }

		//        }
		//        else
		//        {
		//            result.IsSuccess = false;
		//            result.Message = "Sending Mail service is stop.";
		//        }

		//    }
		//    catch (Exception ex)
		//    {
		//        result.IsSuccess = false;

		//        if (ex != null)
		//        {
		//            result.Message = "Error : " + ex.Message.ToString() + Environment.NewLine;

		//            if (ex.InnerException != null)
		//            {
		//                try { result.Message = result.Message + " | InnerException: " + ex.InnerException.ToString().Substring(0, (ex.InnerException.ToString().Length > 1000 ? 1000 : ex.InnerException.ToString().Length)); } catch { result.Message = result.Message + "InnerException: " + ex.InnerException?.ToString(); }
		//            }

		//            if (ex.StackTrace != null)
		//            {
		//                try { result.Message = result.Message + " | StackTrace: " + ex.StackTrace.ToString().Substring(0, (ex.StackTrace.ToString().Length > 1000 ? 1000 : ex.StackTrace.ToString().Length)); } catch { result.Message = result.Message + "InnerException: " + ex.StackTrace?.ToString(); }
		//            }

		//            if (ex.Source != null)
		//            {
		//                try { result.Message = result.Message + " | Source: " + ex.Source.ToString().Substring(0, (ex.Source.ToString().Length > 1000 ? 1000 : ex.Source.ToString().Length)); } catch { result.Message = result.Message + "InnerException: " + ex.Source?.ToString(); }
		//            }

		//            if (ex.StackTrace == null && ex.Source == null)
		//            {
		//                try { result.Message = result.Message + " | Exception: " + ex.ToString().Substring(0, (ex.Source.ToString().Length > 3000 ? 3000 : ex.Source.ToString().Length)); } catch { result.Message = result.Message + "Exception: " + ex?.ToString(); }
		//            }
		//        }

		//    }

		//    //LogService.LogInsert("Common - SendEmail", $"Send Email => Completed at {DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss").Replace("-", "/")} | IsSuccess : {result.IsSuccess} | Message : {result.Message} | Subject : {subject} | To : {string.Join(", ", to_mails)}", null);

		//    return result;
		//}


		public static int? CalculateAge(DateTime? dob = null, DateTime? calculateFromDate = null)
		{
			if (dob == null) return null; // Ensure dob is a valid date
			if (dob == DateTime.MinValue) return null; // Ensure dob is a valid date

			DateTime referenceDate = calculateFromDate ?? DateTime.Today; // Use provided date or default to today

			int age = referenceDate.Year - dob?.Year ?? 0;
			if (referenceDate < dob?.AddYears(age)) age--; // Adjust if birthday hasn't occurred yet

			return age;
		}
	}

	public static class CurrentUser
	{
		public static int UserId { get; set; }
		public static string UserName { get; set; }
		public static int RoleId { get; set; }
		public static string Role { get; set; }
		public static string UserImagePath { get; set; }
	}

	public static class AccessType
	{
		public static int Read => 0;
		public static int Write => 1;
		public static int Delete => 2;

		public static int Get(AccessType_Enum x)
		{
			if (x == AccessType_Enum.Read)
				return Read;
			else if (x == AccessType_Enum.Write)
				return Write;
			//else if (x == AccessType_Enum.Create)
			//    return Create;
			//else if (x == AccessType_Enum.Update)
			//    return Update;
			else
				return Delete;
		}
	};

	public enum AccessType_Enum { Read = 0x0, Write = 0x1, /*Create = 0x1, Update = 0x2, */ Delete = 0x2 };

	public static class AccessControlType
	{
		public static bool Allow => true;
		public static bool Deny => false;
	};

	public class CustomFileOrderComparer : IComparer<string>
	{
		public int Compare(string x, string y)
		{
			// Customize the comparison logic based on your requirements
			if (x.EndsWith("_Delete.json") && !y.EndsWith("_Delete.json"))
			{
				return 1; // x comes after y
			}
			else if (!x.EndsWith("_Delete.json") && y.EndsWith("_Delete.json"))
			{
				return -1; // x comes before y
			}
			else
			{
				// If neither ends with "_Delete.json", or both do, use default string comparison
				return string.Compare(x, y);
			}
		}
	}



}
