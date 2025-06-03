using System.ComponentModel.DataAnnotations.Schema;

namespace Leoz_25;

public partial class AgencyMaster : EntitiesBase
{
	public override long Id { get; set; }

	public long ProjectId { get; set; }

    public string Name { get; set; } = null!;

    public string WorkType { get; set; } = null!;
	[NotMapped] public string WorkType_Text { get; set; }

	public string Status { get; set; } = null!;

	[NotMapped] public string Status_Text { get; set; }

	public string? Notes { get; set; }
}
