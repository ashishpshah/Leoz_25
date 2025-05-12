using System.ComponentModel.DataAnnotations.Schema;

namespace Leoz_25
{
	public partial class RoleMenuAccess : EntitiesBase
	{
		[NotMapped] public override long Id { get; set; }
		public long RoleId { get; set; }
		public long MenuId { get; set; }
		public bool IsCreate { get; set; }
		public bool IsUpdate { get; set; }
		public bool IsRead { get; set; }
		public bool IsDelete { get; set; }

		[NotMapped] public string RoleName { get; set; } = null;
		[NotMapped] public string MenuName { get; set; } = null;
		[NotMapped] public string Area { get; set; } = null;
		[NotMapped] public string Controller { get; set; } = null;
		[NotMapped] public long ParentMenuId { get; set; }
		[NotMapped] public string ParentMenuName { get; set; } = null;
		[NotMapped] public int? DisplayOrder { get; set; } = null;
	}
}
