using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations.Schema;

namespace Leoz_25
{
    public partial class Role : EntitiesBase
	{
		public override long Id { get; set; }
		public string Name { get; set; }
		public int? DisplayOrder { get; set; }
		public bool IsAdmin { get; set; }

		[NotMapped] public long SelectedRoleId { get; set; } = 0;
		[NotMapped] public List<SelectListItem> Menus { get; set; } = null;

	}


}