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

		/// <summary> Designation of an Employee : LOV.DesignationCode when set, else the legacy code / description rules (logged as a warning). </summary>
		public static string ResolveDesignation(LOV? userType, string? code)
		{
			var designationCode = (userType?.DesignationCode ?? "").Trim().ToUpper();

			if (PermissionSubjects.Designations.Contains(designationCode)) return designationCode;

			var designation = ResolveDesignation(code, userType?.LOV_Desc);

			LogService.LogInsert("ProjectAccess - ResolveDesignation", $"Warning : LOV USER_TYPE '{code}' has no valid DesignationCode, designation resolved from code / description as {designation}.");

			return designation;
		}

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

		/// <summary> Client Scope of Work → Add : Vendor only. </summary>
		public static bool CanAddClientScope => IsVendor;

		/// <summary> Material Procurement → Add : Vendor, Customer, Employee (except Architect, Purchaser). </summary>
		public static bool CanAddMaterial => IsVendor || IsCustomer || (IsEmployee && !Is(ARCHITECT) && !Is(PURCHASER));

		/// <summary> Material Procurement → Update (Order / Reject / Edit / Delete) : Manager. </summary>
		public static bool CanUpdateMaterial => Is(MANAGER);

		/// <summary> Material Procurement → Receive : Co-ordinator. </summary>
		public static bool CanReceiveMaterial => Is(COORDINATOR);

		#region Database-driven permissions (ProjectModulePermission, see docs/permissions.md)

		private const string ITEM_PERMISSION_SET = "Leoz.ProjectPermissions";
		private const string ITEM_SHADOW_LOGGED = "Leoz.ProjectPermissions.ShadowLogged";

		/// <summary> appsettings Permissions:Mode : Legacy (default) | Shadow | Dynamic. </summary>
		public static string Mode
		{
			get
			{
				var mode = AppHttpContextAccessor.AppConfiguration?["Permissions:Mode"];

				if (string.Equals(mode, PermissionModes.Dynamic, StringComparison.OrdinalIgnoreCase)) return PermissionModes.Dynamic;
				if (string.Equals(mode, PermissionModes.Shadow, StringComparison.OrdinalIgnoreCase)) return PermissionModes.Shadow;

				return PermissionModes.Legacy;
			}
		}

		public static bool IsDynamic => Mode == PermissionModes.Dynamic;

		/// <summary> Subject of logged-in user : Vendor / Customer = ACTOR, Employee = DESIGNATION. Admin / Super Admin = none (no module, same as HasModule). </summary>
		public static (string? SubjectType, string? SubjectCode) CurrentSubject()
		{
			if (IsVendor) return (PermissionSubjects.ACTOR, PermissionSubjects.VENDOR);
			if (IsCustomer) return (PermissionSubjects.ACTOR, PermissionSubjects.CUSTOMER);
			if (IsEmployee) return (PermissionSubjects.DESIGNATION, Designation);

			return (null, null);
		}

		/// <summary> Permission set kept in session (parsed once per request). </summary>
		public static PermissionSet? SessionPermissions()
		{
			var httpContext = AppHttpContextAccessor.AppHttpContext;
			if (httpContext == null) return null;

			if (httpContext.Items.TryGetValue(ITEM_PERMISSION_SET, out var item) && item is PermissionSet cached) return cached;

			var set = PermissionSet.FromJson(Common.Get_Session(SessionKey.ProjectPermissions));
			if (set != null) httpContext.Items[ITEM_PERMISSION_SET] = set;

			return set;
		}

		public static void StoreSessionPermissions(PermissionSet set)
		{
			Common.Set_Session(SessionKey.ProjectPermissions, set.ToJson());

			var httpContext = AppHttpContextAccessor.AppHttpContext;
			if (httpContext != null) httpContext.Items[ITEM_PERMISSION_SET] = set;
		}

		/// <summary> Database permission of logged-in user on a module action (independent of Mode; use Check at decision points). </summary>
		public static bool Can(string module, string action) => SessionPermissions()?.Can(module, action) ?? false;

		/// <summary> Scope (ALL / OWN) of an allowed action in Dynamic mode; ALL in Legacy / Shadow mode. </summary>
		public static string Scope(string module, string action)
			=> IsDynamic ? (SessionPermissions()?.Scope(module, action) ?? PermissionScopes.ALL) : PermissionScopes.ALL;

		/// <summary>
		/// Single decision point of in-scope Project Detail checks. Legacy : returns the legacy result.
		/// Shadow : returns the legacy result and logs when Can(module, action) differs. Dynamic : returns Can(module, action).
		/// </summary>
		public static bool Check(bool legacyResult, string module, string action)
			=> Check(legacyResult, () => Can(module, action), module + "." + action);

		/// <summary> As Check(legacy, module, action) when the database decision combines several actions. </summary>
		public static bool Check(bool legacyResult, Func<bool> dynamicResult, string label)
		{
			var mode = Mode;

			if (mode == PermissionModes.Legacy) return legacyResult;

			var result = dynamicResult();

			if (mode == PermissionModes.Dynamic) return result;

			if (result != legacyResult) LogShadowMismatch(label, legacyResult, result);

			return legacyResult;
		}

		/// <summary> Module (box) visible : Check(HasModule(code), code, VIEW). </summary>
		public static bool CanView(string module) => !string.IsNullOrEmpty(module) && Check(HasModule(module), module, PermissionActions.VIEW);

		private static void LogShadowMismatch(string label, bool legacyResult, bool result)
		{
			try
			{
				var httpContext = AppHttpContextAccessor.AppHttpContext;

				// Once per request and label (views evaluate the same check for every row)
				if (httpContext != null)
				{
					if (!(httpContext.Items[ITEM_SHADOW_LOGGED] is HashSet<string> logged)) httpContext.Items[ITEM_SHADOW_LOGGED] = logged = new HashSet<string>();
					if (!logged.Add(label)) return;
				}

				var (subjectType, subjectCode) = CurrentSubject();

				LogService.LogInsert("Permissions - Shadow mismatch",
					$"UserId={Common.LoggedUser_Id()} | VendorId={Common.Get_Session_Int(SessionKey.KEY_VENDOR_ID)} | Subject={subjectType}/{subjectCode} | Check={label} | Legacy={legacyResult} | New={result} | Path={httpContext?.Request.Path}");
			}
			catch { }
		}

		#endregion
	}
}
