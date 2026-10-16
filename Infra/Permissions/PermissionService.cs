using Microsoft.Extensions.Caching.Memory;

namespace Leoz_25.Infra
{
	public interface IPermissionService
	{
		/// <summary> Current PermissionVersion (cached 60 s; removed from cache on change). </summary>
		long GetCurrentVersion();

		/// <summary> Effective permissions of a subject for a vendor (tenant). </summary>
		PermissionSet Build(string? subjectType, string? subjectCode, long vendorId);

		/// <summary> Builds the logged-in user's set and stores it in session when missing, stale (version) or forced. </summary>
		void RefreshSession(bool force = false);

		/// <summary> PermissionVersion + 1 (call inside the transaction that changed permission rows). </summary>
		void BumpVersion();

		void InvalidateVersionCache();
	}

	public class PermissionService(IRepositoryWrapper context, IMemoryCache cache) : IPermissionService
	{
		private const string CACHE_KEY_VERSION = "Leoz.PermissionVersion";

		private readonly IRepositoryWrapper _context = context;
		private readonly IMemoryCache _cache = cache;

		public long GetCurrentVersion()
			=> _cache.GetOrCreate(CACHE_KEY_VERSION, entry =>
			{
				entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60);
				return _context.Using<PermissionVersion>().GetByCondition(x => x.Id == 1).Select(x => x.Version).FirstOrDefault();
			});

		public PermissionSet Build(string? subjectType, string? subjectCode, long vendorId)
		{
			var version = GetCurrentVersion();

			if (string.IsNullOrEmpty(subjectType) || string.IsNullOrEmpty(subjectCode)) return PermissionSet.Empty(version);

			var rows = _context.Using<ProjectModulePermission>().GetByCondition(x => x.SubjectType == subjectType && x.SubjectCode == subjectCode
						&& (x.VendorId == null || x.VendorId == vendorId) && x.IsActive && !x.IsDeleted).ToList();

			var activeModules = _context.Using<ProjectModule>().GetByCondition(x => x.IsActive).Select(x => x.Code).ToHashSet();

			return PermissionResolver.Build(rows, subjectType, subjectCode, vendorId, version, activeModules);
		}

		public void RefreshSession(bool force = false)
		{
			if (!Common.IsUserLogged()) return;

			var (subjectType, subjectCode) = ProjectAccess.CurrentSubject();
			var current = ProjectAccess.SessionPermissions();

			if (!force && current != null && current.Version == GetCurrentVersion() && current.SubjectType == subjectType && current.SubjectCode == subjectCode) return;

			var set = Build(subjectType, subjectCode, Common.Get_Session_Int(SessionKey.KEY_VENDOR_ID));

			ProjectAccess.StoreSessionPermissions(set);
		}

		public void BumpVersion()
		{
			var obj = _context.Using<PermissionVersion>().GetByCondition(x => x.Id == 1).FirstOrDefault();

			if (obj == null) _context.Using<PermissionVersion>().Add(new PermissionVersion() { Id = 1, Version = 1 });
			else
			{
				obj.Version = obj.Version + 1;
				_context.Using<PermissionVersion>().Update(obj);
			}
		}

		public void InvalidateVersionCache() => _cache.Remove(CACHE_KEY_VERSION);
	}
}
