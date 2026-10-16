namespace Leoz_25;

public partial class UnitsOfMeasurement
{
	public long Id { get; set; }

	public string Code { get; set; } = null!;

	public string Name { get; set; } = null!;

	public string Category { get; set; } = null!;
}

public partial class LOV
{

	public string LOV_Column { get; set; } = null!;

	public string LOV_Code { get; set; } = null!;

	public string LOV_Desc { get; set; } = null!;
	public string? ProjectDetailTypeAccess { get; set; } = null!;
	public int DisplayOrder { get; set; }

	/// <summary> Canonical designation of USER_TYPE rows (MANAGER, COORDINATOR, ...), used by Project Detail permissions. </summary>
	public string? DesignationCode { get; set; }
}
