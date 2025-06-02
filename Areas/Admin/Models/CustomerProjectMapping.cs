namespace Leoz_25;

public partial class CustomerProjectMapping : EntitiesBase
{
	public override long Id { get; set; }

	public long VendorId { get; set; }

	public long CustomerId { get; set; }

    public long ProjectId { get; set; }
}

public partial class EmployeeProjectMapping : EntitiesBase
{
	public override long Id { get; set; }

	public long VendorId { get; set; }

	public long EmployeeId { get; set; }

    public long ProjectId { get; set; }
}
