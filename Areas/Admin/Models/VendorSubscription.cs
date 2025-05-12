using System.ComponentModel.DataAnnotations.Schema;

namespace Leoz_25;

public class VendorSubscription : EntitiesBase
{
	public override long Id { get; set; }
	public long VendorId { get; set; }
	public long PackageId { get; set; }
	public DateTime StartDate { get; set; }
	public DateTime EndDate { get; set; }
	public bool IsCancelled { get; set; }


	[NotMapped] public string StartDate_Text { get; set; }
	[NotMapped] public string EndDate_Text { get; set; }

	[NotMapped] public Package Selected_Package { get; set; }

}