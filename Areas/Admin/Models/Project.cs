using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace Leoz_25;

public partial class Project : EntitiesBase
{
	public override long Id { get; set; }

	public long VendorId { get; set; }

	public string Name { get; set; } = null!;

	public string? Description { get; set; }

	public DateTime StartDate { get; set; }

	public DateTime? HandoverDate { get; set; }

	[NotMapped] public string StartDate_Text { get; set; }
	[NotMapped] public string HandoverDate_Text { get; set; }

	public string? Address { get; set; }

	public long? CityId { get; set; }

	public long? StateId { get; set; }

	public long? CountryId { get; set; }

	public string? LocationLink { get; set; }
	public long? CoordinatorId { get; set; }
	[NotMapped] public string CoordinatorName { get; set; }

	public string? SiteDetails { get; set; }
}
