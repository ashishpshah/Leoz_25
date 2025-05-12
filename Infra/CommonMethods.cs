using Microsoft.AspNetCore.Mvc.Rendering;
using System.Globalization;

namespace Leoz_25.Infra
{
	public static class StringExtension
	{
		public static string ToCamelCase(this string str)
		{
			try
			{
				if (!string.IsNullOrEmpty(str))
					return new CultureInfo("en-IN", false).TextInfo.ToTitleCase(str.ToLower().Trim());
			}
			catch { }

			return str;
		}
	}

	public static class CommonMethods
	{
		public static string ToCamelCase(string str) => new CultureInfo("en-IN", false).TextInfo.ToTitleCase(str.ToLower());

		public static string GetMonthName(int mon) { try { return DateTimeFormatInfo.CurrentInfo.MonthNames[mon - 1]; } catch { return ""; } }

		public static string GetMonthName(string mon) { try { return GetMonthName(Convert.ToInt32(mon)); } catch { return mon; } }

		//public static IsoDateTimeConverter ConverterIsoDateTime()
		//{
		//	return new IsoDateTimeConverter { DateTimeFormat = "dd/MM/yyyy" };
		//}

		public static SelectList CreateSelectList(SelectList list, object SelectedValue)
		{
			List<SelectListItem> _list = list.ToList();
			_list.Insert(0, new SelectListItem() { Value = "", Text = "Select" });

			return new SelectList((IEnumerable<SelectListItem>)_list, "Value", "Text", SelectedValue);
		}

		//public static System.Drawing.Image ByteArrayToImage(byte[] imageBytes)
		//{
		//	MemoryStream ms = new MemoryStream(imageBytes, 0, imageBytes.Length);
		//	ms.Write(imageBytes, 0, imageBytes.Length);
		//	System.Drawing.Image image = System.Drawing.Image.FromStream(ms, true);
		//	return image;
		//}

		//public static System.Drawing.Image Base64ToImage(string base64String)
		//{
		//	byte[] imageBytes = Convert.FromBase64String(base64String);
		//	MemoryStream ms = new MemoryStream(imageBytes, 0, imageBytes.Length);
		//	ms.Write(imageBytes, 0, imageBytes.Length);
		//	System.Drawing.Image image = System.Drawing.Image.FromStream(ms, true);
		//	return image;
		//}


		//public static byte[] ImageToByteArray(System.Drawing.Image img)
		//{
		//	MemoryStream ms = new MemoryStream();
		//	img.Save(ms, img.RawFormat);
		//	return ms.ToArray();
		//}


		//public static string ImageToString(System.Drawing.Image img)
		//{
		//	string FileExtension = img.RawFormat.ToString();
		//	MemoryStream ms = new MemoryStream();
		//	img.Save(ms, img.RawFormat);
		//	string imgString = Convert.ToBase64String(ms.ToArray());
		//	return Convert.ToString("data:image/." + FileExtension + ";base64,") + imgString;
		//}


		//public static string ByteArrayToString(byte[] bytes, string name = null, string fileName = null, string contentType = null)
		//{
		//	string FileExtension = "";

		//	string imgString = "";

		//	System.Drawing.Image img = null;

		//	if (bytes != null && bytes.Length > 0)
		//	{
		//		using (var stream = new MemoryStream(bytes))
		//		{
		//			//var file = new FormFile(stream, 0, bytes.Length, name, fileName)
		//			//{
		//			//	Headers = new HeaderDictionary(),
		//			//	ContentType = contentType,
		//			//};

		//			//System.Net.Mime.ContentDisposition cd = new System.Net.Mime.ContentDisposition { FileName = fileName };

		//			//file.ContentDisposition = cd.ToString();

		//			//using (var ms = new MemoryStream())
		//			//{
		//			//	file.CopyTo(ms);

		//			//	return Convert.ToString("data:application/octet-stream;base64,") + Convert.ToBase64String(ms.ToArray());
		//			//}

		//			return null;
		//		}
		//	}
		//	else
		//	{
		//		string DefaultImagePath = MyServer.MapPath("\\images\\no_image_available.png");

		//		var imageBytes = System.IO.File.ReadAllBytes(DefaultImagePath);
		//		MemoryStream memoryStream = new MemoryStream(imageBytes, 0, imageBytes.Length);
		//		memoryStream.Write(imageBytes, 0, imageBytes.Length);
		//		img = System.Drawing.Image.FromStream(memoryStream, true);

		//		FileExtension = img.RawFormat.ToString();

		//		imgString = Convert.ToBase64String(imageBytes);
		//		return Convert.ToString("data:image/." + FileExtension + ";base64,") + imgString;
		//	}
		//}

		//public static string PathToBase64String(string path)
		//{
		//	if (!string.IsNullOrEmpty(path))
		//	{
		//		byte[] imageArray = System.IO.File.ReadAllBytes(path);
		//		return ByteArrayToString(imageArray);
		//	}
		//	else
		//	{
		//		string DefaultImagePath = MyServer.MapPath("\\No_Image_Available.jpg");

		//		byte[] imageArray = System.IO.File.ReadAllBytes(DefaultImagePath);
		//		return ByteArrayToString(imageArray);
		//	}
		//}
	}

	public class SelectListItem_Custom
	{
		public string Text { get; set; }
		public string Value { get; set; }
		public string Value2 { get; set; }
		public string Value3 { get; set; }
		public int OrderBy { get; set; }
		public string Group { get; set; }

		public SelectListItem_Custom(string value, string text)
		{
			Value = value;
			Text = text;
		}

		public SelectListItem_Custom(string value, string text, int orderBy)
		{
			Value = value;
			Text = text;
			OrderBy = orderBy;
		}

		public SelectListItem_Custom(string value, string text, string group)
		{
			Value = value;
			Text = text;
			Group = group;
		}

		public SelectListItem_Custom(string value, string text, int orderBy, string group)
		{
			Value = value;
			Text = text;
			OrderBy = orderBy;
			Group = group;
		}
		
		public SelectListItem_Custom(string value, string text, int orderBy, string otherData, string group)
		{
			Value = value;
			Text = text;
			OrderBy = orderBy;
			Value2 = otherData;
			Group = group;
		}

		public SelectListItem_Custom(string value, string text, string otherData, string group)
		{
			Value = value;
			Text = text;
			Value2 = otherData;
			Group = group;
		}
		public SelectListItem_Custom(string value, string text, string otherData, string extraData, string group)
		{
			Value = value;
			Text = text;
			Value2 = otherData;
			Value3 = extraData;
			Group = group;
		}

	}

	public static class MyServer
	{
		public static string MapPath(string path)
		{
			return Path.GetFullPath("wwwroot").ToString() + "\\" + path;
		}
	}


	public class JqueryDatatableParam
	{
		public string sEcho { get; set; }
		public string sSearch { get; set; }
		public int iDisplayLength { get; set; }
		public int iDisplayStart { get; set; }
		public int iColumns { get; set; }
		public int iSortCol_0 { get; set; }
		public string sSortDir_0 { get; set; }
		public int iSortingCols { get; set; }
		public string sColumns { get; set; }
	}

}