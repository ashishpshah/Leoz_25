
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace Leoz_25
{
	public partial class Menu : EntitiesBase
	{
		public override long Id { get; set; }
		public long ParentId { get; set; }
		public string? Name { get; set; }
		[NotMapped] public string ParentMenuName { get; set; }
		public string? Icon { get; set; }
		//public string Url { get; set; }
		public string? Area { get; set; }
		public string? Controller { get; set; }
		public string? Url { get; set; }
		public bool IsSuperAdmin { get; set; }
		public bool IsAdmin { get; set; }
		public int? DisplayOrder { get; set; }
	}
}
