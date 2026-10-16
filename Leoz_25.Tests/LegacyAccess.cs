using Leoz_25.Infra;

namespace Leoz_25.Tests;

/// <summary>
/// Pure copy of the OLD add / edit / workflow rules (Infra/ProjectAccess.cs and ProjectController before the
/// permission tables), as a function of (subject, module, action). Do not "fix" it : it is the parity reference.
/// </summary>
internal static class LegacyAccess
{
	private const string V = PermissionSubjects.VENDOR, C = PermissionSubjects.CUSTOMER;
	private const string MNGR = ProjectAccess.MANAGER, COORD = ProjectAccess.COORDINATOR, SS = ProjectAccess.SITE_SUPERVISOR,
		SE = ProjectAccess.SITE_ENGINEER, PUR = ProjectAccess.PURCHASER, ARCH = ProjectAccess.ARCHITECT, SV = ProjectAccess.SITE_VENDOR;

	// ProjectAccess.CanUpload / CanModify / CanApproveDoc / CanAddClientScope / CanAddMaterial / CanUpdateMaterial / CanReceiveMaterial
	private static bool CanUpload(string s) => s is V or C or MNGR or COORD or SE or ARCH;
	private static bool CanModify(string s) => s is V or C or MNGR;
	private static bool CanApproveDoc(string s) => CanModify(s) || s == COORD;
	private static bool CanAddClientScope(string s) => s == V;
	private static bool CanAddMaterial(string s) => s is V or C || (PermissionSubjects.Designations.Contains(s) && s is not ARCH and not PUR);
	private static bool IsManager(string s) => s == MNGR;
	private static bool IsCoordinator(string s) => s == COORD;

	/// <param name="employeeView">Employee designation → module codes visible through LOV.ProjectDetailTypeAccess (legacy HasModule).</param>
	public static bool Can(string subject, string module, string action, IReadOnlyDictionary<string, HashSet<string>> employeeView)
	{
		// Daily Update screen : no server check before (menu only); Save_DailyUpdate : Employee.UserType = "COORD" (COORD ⇔ COORDINATOR, report R7 of 003)
		if (module == PermissionModules.DAILY)
			return action == PermissionActions.VIEW || (action is PermissionActions.ADD or PermissionActions.EDIT && IsCoordinator(subject));

		// "Recent Uploads" of F3D / DRW / MB : shown to everyone who could open the module
		if (action == PermissionActions.VIEW || (action == PermissionActions.HISTORY && PermissionModules.HistoryDocuments.Contains(module)))
			return subject is V or C || (employeeView.TryGetValue(subject, out var modules) && modules.Contains(module));

		if (PermissionModules.Documents.Contains(module))
		{
			var isHistory = PermissionModules.HistoryDocuments.Contains(module);

			return action switch
			{
				// Save_Doc Id == 0 : CanUpload, WCLTSC also CanAddClientScope
				PermissionActions.ADD => CanUpload(subject) && (module != PermissionModules.WCLTSC || CanAddClientScope(subject)),
				// Save_Doc Id > 0 : CanUpload, non-history types also CanModify
				PermissionActions.EDIT => CanUpload(subject) && (isHistory || CanModify(subject)),
				// Save_Doc_Status "DOC"
				PermissionActions.APPROVE or PermissionActions.REJECT => CanApproveDoc(subject),
				_ => false
			};
		}

		return module switch
		{
			// Save_WP : never Customer; Save_Doc_Status "WP" : CanModify
			PermissionModules.WPEND => action switch
			{
				PermissionActions.ADD => subject != C && CanUpload(subject),
				PermissionActions.EDIT => subject != C && CanModify(subject),
				PermissionActions.COMPLETE or PermissionActions.REJECT => CanModify(subject),
				_ => false
			},

			// Save_MP / Save_Doc_Status "MP"
			PermissionModules.MPRO => action switch
			{
				PermissionActions.ADD => CanAddMaterial(subject),
				PermissionActions.EDIT or PermissionActions.ORDER or PermissionActions.REJECT => IsManager(subject),
				PermissionActions.RECEIVE => IsCoordinator(subject),
				_ => false
			},

			// Save_WCR : Manager add / edit, Customer approve. Save_Doc_Status "WC" had NO role check :
			// the seed (Customer only) is the documented intentional tightening, so the reference is the Save_WCR / UI rule.
			PermissionModules.WCOMP => action switch
			{
				PermissionActions.ADD or PermissionActions.EDIT => IsManager(subject),
				PermissionActions.APPROVE or PermissionActions.REJECT => subject == C,
				_ => false
			},

			_ => false
		};
	}

	/// <summary> Legacy HasModule : CSV split on ',', trimmed, "ALL" or exact (case-sensitive) module code. </summary>
	public static HashSet<string> ParseModules(string? csv, IEnumerable<string> allModules)
	{
		var tokens = (csv ?? "").Split(',').Select(x => x.Trim()).ToList();

		return allModules.Where(m => tokens.Any(t => t == "ALL" || t == m)).ToHashSet();
	}
}
