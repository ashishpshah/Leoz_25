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
	public int DisplayOrder { get; set; }
}
