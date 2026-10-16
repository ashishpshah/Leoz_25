namespace Leoz_25;

/// <summary> Project Detail module (box) : EPM, F3D, DRW, WCLTSC, MB, SITE, WPEND, MPRO, WCOMP. </summary>
public partial class ProjectModule
{
	public string Code { get; set; } = null!;

	public string Name { get; set; } = null!;

	public int DisplayOrder { get; set; }

	public bool IsActive { get; set; }
}

/// <summary> VIEW, ADD, EDIT, DELETE, APPROVE, REJECT, COMPLETE, ORDER, RECEIVE, DELETE_FILE. </summary>
public partial class PermissionAction
{
	public string Code { get; set; } = null!;

	public string Name { get; set; } = null!;
}

/// <summary> Actions which exist for a module. </summary>
public partial class ProjectModuleAction
{
	public string ModuleCode { get; set; } = null!;

	public string ActionCode { get; set; } = null!;
}

/// <summary> Right of a subject (ACTOR : VENDOR / CUSTOMER, DESIGNATION : MANAGER, COORDINATOR, ...) on a module action. VendorId NULL = system default, else vendor override. </summary>
public partial class ProjectModulePermission : EntitiesBase
{
	public override long Id { get; set; }

	public long? VendorId { get; set; }

	public string SubjectType { get; set; } = null!;

	public string SubjectCode { get; set; } = null!;

	public string ModuleCode { get; set; } = null!;

	public string ActionCode { get; set; } = null!;

	public bool IsAllowed { get; set; }

	public string Scope { get; set; } = null!;
}

/// <summary> Single row (Id = 1), bumped on every permission change. </summary>
public partial class PermissionVersion
{
	public int Id { get; set; }

	public long Version { get; set; }
}
