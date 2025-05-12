using Microsoft.AspNetCore.Mvc.Rendering;
using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Leoz_25.Infra.Services
{
	public class ValidateField
	{
		//public bool Validate(string type, object value)
		//{
		//    switch (type)
		//    {
		//        case "EMAIL":
		//            return Regex.IsMatch(Convert.ToString(value), @"\A(?:[a-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[a-z0-9!#$%&'*+/=?^_`{|}~-]+)*@(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+[a-z0-9](?:[a-z0-9-]*[a-z0-9])?)\Z", RegexOptions.IgnoreCase); ;
		//        default:
		//            return false;
		//    }


		//    return false;
		//}

		public static bool IsNonAlphaNumeric(string strIn) => Regex.IsMatch((strIn ?? ""), @"[^\w\.@-]");

		public static bool IsDecimal(string strIn) => Regex.IsMatch((strIn ?? ""), @"[^[0-9]*(\.[0-9]{0,2})?$]");

		public static bool IsValidMobileNo(string strIn) => Regex.IsMatch((strIn ?? ""), @"^[0][1-9]\d{9}$|^[1-9]\d{9}$");

		public static bool IsValidMobileNo_D10(string strIn) => Regex.IsMatch((strIn ?? ""), @"^[0][1-9]\d{9}$|^[1-9]\d{9}$") && strIn?.Length == 10;

		public static bool IsValidBirth_Date(string Birth_Date)
		{

			string date = DateTime.UtcNow.ToString("dd-MM-yyyy");
			var parts = date.ToString().Split('-');
			var currentyear = parts[2];
			var currentmonth = parts[1];
			var currentday = parts[0];
			var splituseryear = Birth_Date.ToString().Split('-');
			var useryear = splituseryear[0];
			var usermonth = splituseryear[1];
			var userday = splituseryear[2];
			if (Convert.ToInt32(currentyear) - Convert.ToInt32(useryear) < 18)
			{
				return false;
			}
			if (Convert.ToInt32(currentyear) - Convert.ToInt32(useryear) == 18)
			{
				//CD: 11/06/2018 and DB: 15/07/2000. Will turned 18 on 15/07/2018.
				if (Convert.ToInt32(currentmonth) < Convert.ToInt32(usermonth))
				{
					return false;
				}
				if (Convert.ToInt32(currentmonth) == Convert.ToInt32(usermonth))
				{
					//CD: 11/06/2018 and DB: 15/06/2000. Will turned 18 on 15/06/2018.
					if (Convert.ToInt32(currentday) < Convert.ToInt32(userday))
					{
						return false;
					}
				}
			}
			return true;
		}

        public static bool IsValidUrl(string url)
        {
            string pattern = @"^(https?://)?(www\.)?[\w-]+(\.[a-z]{2,})+(/[\w-]*)*$";
            Regex regex = new Regex(pattern, RegexOptions.IgnoreCase);
            return regex.IsMatch(url);
        }

        public static bool IsValidEmail(string email)
		{
			if (string.IsNullOrWhiteSpace(email))
				return false;

			try
			{
				// Normalize the domain
				email = Regex.Replace(email, @"(@)(.+)$", DomainMapper, RegexOptions.None, TimeSpan.FromMilliseconds(200));

				// Examines the domain part of the email and normalizes it.
				string DomainMapper(Match match)
				{
					// Use IdnMapping class to convert Unicode domain names.
					var idn = new IdnMapping();

					// Pull out and process domain name (throws ArgumentException on invalid)
					string domainName = idn.GetAscii(match.Groups[2].Value);

					return match.Groups[1].Value + domainName;
				}
			}
			catch (RegexMatchTimeoutException e)
			{
				return false;
			}
			catch (ArgumentException e)
			{
				return false;
			}

			try
			{
				if (email.Contains(","))
				{
					foreach (var item in email.Split(','))
					{
						if (Regex.IsMatch(item,
						@"\A(?:[a-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[a-z0-9!#$%&'*+/=?^_`{|}~-]+)*@(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+[a-z0-9](?:[a-z0-9-]*[a-z0-9])?)\Z",
						RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(250))) continue;
						else { break; return false; }
					}
					return true;
				}
				else
					return Regex.IsMatch(email,
						@"\A(?:[a-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[a-z0-9!#$%&'*+/=?^_`{|}~-]+)*@(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+[a-z0-9](?:[a-z0-9-]*[a-z0-9])?)\Z",
						RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(250));
			}
			catch (RegexMatchTimeoutException)
			{
				return false;
			}
		}
		public static bool IsValidGST(string strIn) => Regex.IsMatch((strIn.ToUpper() ?? ""), @"^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z]{1}[1-9A-Z]{1}Z[0-9A-Z]{1}$");
		public static bool IsValidPanNo(string strIn) => Regex.IsMatch((strIn.ToUpper() ?? ""), @"^[A-Z]{5}\d{4}[A-Z]{1}$");
		public static List<SelectListItem> ToSelectList(DataTable table, string valueField, string textField)
		{
			try
			{
				List<SelectListItem> list = new List<SelectListItem>();

				list.Add(new SelectListItem() { Text = "-- Select --", Value = "0" });

				foreach (DataRow row in table.Rows)
					list.Add(new SelectListItem() { Text = row[textField].ToString(), Value = row[valueField].ToString() });

				return list;
			}
			catch (Exception ex)
			{
				//string actionName = "ToSelectList1";
				//string controllerName = "DBContext";
				// LogEntry.InsertLogEntry(controllerName + '_' + actionName, "", ex);
				return null;
			}


		}
	}
}
