using System.ComponentModel.DataAnnotations.Schema;

namespace Leoz_25;

public partial class ProjectSiteMaterial : EntitiesBase
{
	public override long Id { get; set; }

	public long ProjectId { get; set; }

	public long CustomerId { get; set; }

	public string MaterialFor { get; set; } = null!;

	public string MaterialName { get; set; } = null!;

	public string? MaterialCode { get; set; }

	public string? MaterialBrand { get; set; }

	public decimal Qty { get; set; }
	public decimal? Qty_Order { get; set; }

	public string UOM { get; set; } = null!;

	[NotMapped] public string UOM_Text { get; set; }

	public string Action { get; set; } = null!;

	[NotMapped] public string Action_Text { get; set; }

	public string Status { get; set; } = null!;

	[NotMapped] public string Status_Text { get; set; }

	public DateTime StatusDate { get; set; }

	[NotMapped] public string StatusDate_Text { get; set; }

}
