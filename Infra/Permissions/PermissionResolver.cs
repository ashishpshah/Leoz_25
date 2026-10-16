namespace Leoz_25.Infra
{
	/// <summary> Pure merge rules of ProjectModulePermission rows (no database / session access, unit tested). </summary>
	public static class PermissionResolver
	{
		/// <summary>
		/// Effective permissions of one subject for one vendor (tenant) :
		/// system default rows (VendorId NULL) overridden by the vendor's rows (VendorId = vendorId), for the same module / action.
		/// Inactive / soft-deleted rows are ignored. Rows of inactive modules are ignored. Missing = not allowed.
		/// </summary>
		public static PermissionSet Build(IEnumerable<ProjectModulePermission> rows, string subjectType, string subjectCode, long vendorId, long version, ICollection<string>? activeModules = null)
		{
			var set = PermissionSet.Empty(version);

			set.SubjectType = subjectType ?? "";
			set.SubjectCode = subjectCode ?? "";

			if (rows == null || string.IsNullOrEmpty(subjectType) || string.IsNullOrEmpty(subjectCode)) return set;

			var effective = Effective(rows.Where(x => x.SubjectType == subjectType && x.SubjectCode == subjectCode), vendorId);

			foreach (var row in effective.Values.Where(x => x.IsAllowed))
			{
				if (activeModules != null && !activeModules.Contains(row.ModuleCode)) continue;

				set.Allow(row.ModuleCode, row.ActionCode, row.Scope);
			}

			return set;
		}

		/// <summary> (module, action) → winning row : vendor override when present, else system default. </summary>
		public static Dictionary<(string Module, string Action), ProjectModulePermission> Effective(IEnumerable<ProjectModulePermission> subjectRows, long vendorId)
		{
			var result = new Dictionary<(string, string), ProjectModulePermission>();

			var valid = subjectRows.Where(x => x.IsActive && !x.IsDeleted).ToList();

			foreach (var row in valid.Where(x => x.VendorId == null))
				result[(row.ModuleCode, row.ActionCode)] = row;

			if (vendorId > 0)
				foreach (var row in valid.Where(x => x.VendorId == vendorId))
					result[(row.ModuleCode, row.ActionCode)] = row;

			return result;
		}
	}
}
