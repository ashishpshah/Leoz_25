using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Leoz_25;

public partial class ProjectDailyUpdateFiles : EntitiesBase
{
	public override long Id { get; set; }

	public long DailyUpdateId { get; set; }

	public string? FileName { get; set; }

	public string? FileContentType { get; set; }

	public byte[]? FileData { get; set; }

	[NotMapped] public virtual long CreatedBy { get; set; }
	[NotMapped] public virtual Nullable<System.DateTime> CreatedDate { get; set; }
	[NotMapped] public virtual long LastModifiedBy { get; set; }
	[NotMapped]public virtual Nullable<System.DateTime> LastModifiedDate { get; set; }
	[NotMapped]public virtual bool IsActive { get; set; }
	[NotMapped] public virtual bool IsDeleted { get; set; }
	
}
