namespace Leoz_25.Infra
{
	/// <summary> Module codes (table ProjectModule) : the 9 Project Detail boxes and the Daily Update screen. </summary>
	public static class PermissionModules
	{
		public const string EPM = "EPM";
		public const string F3D = "F3D";
		public const string DRW = "DRW";
		public const string WCLTSC = "WCLTSC";
		public const string MB = "MB";

		/// <summary> Project Detail box "Site Updates" : document module (ProjectSiteDoc Type SITE). </summary>
		public const string SITE = "SITE";

		public const string WPEND = "WPEND";
		public const string MPRO = "MPRO";
		public const string WCOMP = "WCOMP";

		/// <summary> Daily Update screen (menu Daily Update, ProjectDailyUpdate) : not a Project Detail box. </summary>
		public const string DAILY = "DAILY";

		/// <summary> Document modules (entity ProjectSiteDoc, Partial_AddEditForm_Doc / Save_Doc / Save_Doc_Status "DOC"). </summary>
		public static readonly string[] Documents = { EPM, F3D, DRW, WCLTSC, MB, SITE };

		/// <summary> Document modules which keep only the latest version (re-upload replaces the file). </summary>
		public static readonly string[] HistoryDocuments = { F3D, DRW, MB };
	}

	/// <summary> Actions (table PermissionAction). Delete is NOT a permission : delete endpoints keep their legacy checks. </summary>
	public static class PermissionActions
	{
		public const string VIEW = "VIEW";
		public const string ADD = "ADD";
		public const string EDIT = "EDIT";
		public const string APPROVE = "APPROVE";
		public const string REJECT = "REJECT";
		public const string COMPLETE = "COMPLETE";
		public const string ORDER = "ORDER";
		public const string RECEIVE = "RECEIVE";

		public static readonly string[] All = { VIEW, ADD, EDIT, APPROVE, REJECT, COMPLETE, ORDER, RECEIVE };
	}

	/// <summary> Permission subjects : ACTOR (Vendor / Customer login) or DESIGNATION (Employee login). </summary>
	public static class PermissionSubjects
	{
		public const string ACTOR = "ACTOR";
		public const string DESIGNATION = "DESIGNATION";

		public const string VENDOR = "VENDOR";
		public const string CUSTOMER = "CUSTOMER";

		public static readonly string[] Actors = { VENDOR, CUSTOMER };

		/// <summary> Canonical designations (LOV.DesignationCode). </summary>
		public static readonly string[] Designations =
		{
			ProjectAccess.MANAGER, ProjectAccess.COORDINATOR, ProjectAccess.SITE_SUPERVISOR, ProjectAccess.SITE_ENGINEER,
			ProjectAccess.PURCHASER, ProjectAccess.ARCHITECT, ProjectAccess.SITE_VENDOR, ProjectAccess.OTHER
		};

		public static string TypeOf(string subjectCode) => Actors.Contains(subjectCode) ? ACTOR : DESIGNATION;

		public static bool IsValid(string subjectType, string subjectCode)
			=> (subjectType == ACTOR && Actors.Contains(subjectCode)) || (subjectType == DESIGNATION && Designations.Contains(subjectCode));

		public static string DisplayName(string subjectCode) => subjectCode switch
		{
			VENDOR => "Vendor",
			CUSTOMER => "Customer",
			ProjectAccess.MANAGER => "Manager",
			ProjectAccess.COORDINATOR => "Co-Ordinator",
			ProjectAccess.SITE_SUPERVISOR => "Site Supervisor",
			ProjectAccess.SITE_ENGINEER => "Site Engineer",
			ProjectAccess.PURCHASER => "Purchaser",
			ProjectAccess.ARCHITECT => "Architect",
			ProjectAccess.SITE_VENDOR => "Site Vendor",
			ProjectAccess.OTHER => "Other",
			_ => subjectCode
		};
	}

	/// <summary> appsettings Permissions:Mode. </summary>
	public static class PermissionModes
	{
		/// <summary> Legacy rules decide (default). </summary>
		public const string Legacy = "Legacy";

		/// <summary> Legacy rules decide, database permissions are also evaluated and every mismatch is logged. </summary>
		public const string Shadow = "Shadow";

		/// <summary> Database permissions decide. </summary>
		public const string Dynamic = "Dynamic";
	}

	public static class PermissionScopes
	{
		public const string ALL = "ALL";

		/// <summary> Only records the user created / last modified. </summary>
		public const string OWN = "OWN";
	}
}
