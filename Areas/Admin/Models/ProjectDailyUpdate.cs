using System.ComponentModel.DataAnnotations.Schema;

namespace Leoz_25;

public partial class ProjectDailyUpdate : EntitiesBase
{
	public override long Id { get; set; }

	public long ProjectId { get; set; }

	[NotMapped] public string Project_Name { get; set; }

	public long CustomerId { get; set; }
	[NotMapped] public string Customer_Name { get; set; }

	public string Notes { get; set; } = null!;
	public virtual Nullable<System.DateTime> Date { get; set; }
	[NotMapped] public virtual string Date_Text { get; set; }

	public string? FilePath { get; set; }
}
