using System.ComponentModel.DataAnnotations.Schema;

namespace Leoz_25;

public partial class ProjectSiteDoc : EntitiesBase
{
    public override long Id { get; set; }
    public long ProjectId { get; set; }

    public DateTime UploadDate { get; set; }

    [NotMapped] public string UploadDate_Text { get; set; }

    public string? FilePath { get; set; }

	public string Remark { get; set; } = null!;

    public string Type { get; set; } = null!;

	[NotMapped] public string Type_Text { get; set; } = null!;

    public string Status { get; set; } = null!;

	[NotMapped] public string Status_Text { get; set; } = null!;

	public DateTime StatusDate { get; set; }
	[NotMapped] public string StatusDate_Text { get; set; }
}
