using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace Leoz_25;

public partial class Package : EntitiesBase
{
	public override long Id { get; set; }
	public long VendorId { get; set; }

	public string Name { get; set; } = null!;

	public string? Description { get; set; }

	public decimal Price { get; set; }

	public bool IsYearly { get; set; }
	public int DurationInDays { get; set; }

	public bool IsProjectBased { get; set; }
	public int ProjectLimit { get; set; }
	[NotMapped] public string Option { get; set; }
}
