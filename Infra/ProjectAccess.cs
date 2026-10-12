namespace Leoz_25.Infra
{
	/// <summary>
	/// Role-based access for Project Detail modules, as per the "Vendor Management System – Process & Data Flow" document.
	/// Employee designation is resolved once at login (from LOV USER_TYPE code / description) and kept in session.
	/// </summary>
	public static class ProjectAccess
	{
		public const string MANAGER = "MANAGER";
		public const string COORDINATOR = "COORDINATOR";
		public const string SITE_SUPERVISOR = "SITE_SUPERVISOR";
		public const string SITE_ENGINEER = "SITE_ENGINEER";
		public const string PURCHASER = "PURCHASER";
		public const string ARCHITECT = "ARCHITECT";
		public const string SITE_VENDOR = "SITE_VENDOR";
		public const string OTHER = "OTHER";

		public static string KEY_DESIGNATION => "Employee_Designation";
		public static string KEY_DESIGNATION_NAME => "Employee_Designation_Name";

		/// <summary> Shown in header after user name : Employee designation (Manager, Co-Ordinator, ...) only for Employee login. </summary>
		public static string Header_Designation => IsEmployee ? (Common.Get_Session(KEY_DESIGNATION_NAME) ?? "") : "";

		/// <summary> Map LOV USER_TYPE (code + description) to a designation. Unknown designations get view-only access. </summary>
		public static string ResolveDesignation(string code, string description)
		{
			code = (code ?? "").Trim().ToUpper();
			var desc = (description ?? "").Trim().ToLower().Replace("-", "").Replace(" ", "");

			if (code == "MNGR" || desc.Contains("manager")) return MANAGER;
			if (code == "COORD" || desc.Contains("coordinator")) return COORDINATOR;
			if (code == "PUR" || desc.Contains("purchase")) return PURCHASER;
			if (desc.Contains("supervisor")) return SITE_SUPERVISOR;
			if (desc.Contains("engineer")) return SITE_ENGINEER;
			if (desc.Contains("architect")) return ARCHITECT;
			if (desc.Contains("vendor")) return SITE_VENDOR;

			return OTHER;
		}

		private static bool IsVendor => Common.Get_Session_Int(SessionKey.KEY_IS_VENDOR) == 1;
		private static bool IsCustomer => Common.Get_Session_Int(SessionKey.KEY_IS_CUSTOMER) == 1;
		private static bool IsEmployee => Common.Get_Session_Int(SessionKey.KEY_IS_EMPLOYEE) == 1;

		public static string Designation => IsEmployee ? (Common.Get_Session(KEY_DESIGNATION) ?? OTHER) : "";

		public static bool Is(string designation) => IsEmployee && Designation == designation;

		/// <summary> Module (box) is allowed for logged-in user. Vendor/Customer see all; Employee as per LOV ProjectDetailTypeAccess. </summary>
		public static bool HasModule(string type)
		{
			if (IsVendor || IsCustomer) return true;
			if (!IsEmployee || string.IsNullOrEmpty(type)) return false;

			var access = Common.Get_Session(SessionKey.ProjectDetailTypeAccess) ?? "";

			return access.Split(',').Select(x => x.Trim()).Any(x => x == "ALL" || x == type);
		}

		/// <summary> Upload / Add new record in document modules : Manager, Co-ordinator, Site Engineer, Architect (+ Vendor, Customer). </summary>
		public static bool CanUpload => IsVendor || IsCustomer || Is(MANAGER) || Is(COORDINATOR) || Is(SITE_ENGINEER) || Is(ARCHITECT);

		/// <summary> Edit / Delete / Change status : Manager (control level) (+ Vendor, Customer). </summary>
		public static bool CanModify => IsVendor || IsCustomer || Is(MANAGER);

		/// <summary> Approve / Reject uploaded documents : Manager, Co-ordinator (+ Vendor, Customer). </summary>
		public static bool CanApproveDoc => CanModify || Is(COORDINATOR);

		/// <summary> Material Procurement → Add : Co-ordinator. </summary>
		public static bool CanAddMaterial => Is(COORDINATOR);

		/// <summary> Material Procurement → Update (Order / Reject / Edit / Delete) : Manager. </summary>
		public static bool CanUpdateMaterial => Is(MANAGER);

		/// <summary> Material Procurement → Receive : Co-ordinator. </summary>
		public static bool CanReceiveMaterial => Is(COORDINATOR);
	}
}
