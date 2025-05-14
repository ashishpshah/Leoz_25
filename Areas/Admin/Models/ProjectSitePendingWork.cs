using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace Leoz_25;

public partial class ProjectSitePendingWork : EntitiesBase
{
	public override long Id { get; set; }

	public long ProjectId { get; set; }

    public long CustomerId { get; set; }

    public DateTime UploadDate { get; set; }

	[NotMapped] public string UploadDate_Text { get; set; }

	public string PendingFrom { get; set; } = null!;

    public string Remarks { get; set; } = null!;

    public string PendingPoint { get; set; } = null!;


	public string Action { get; set; } = null!;

	[NotMapped] public string Action_Text { get; set; }

	public string Status { get; set; } = null!;

	[NotMapped] public string Status_Text { get; set; }

	public DateTime StatusDate { get; set; }

	[NotMapped] public string StatusDate_Text { get; set; }
}
