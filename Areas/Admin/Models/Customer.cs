using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace Leoz_25
{
	public partial class Customer : EntitiesBase
	{
		public override long Id { get; set; }
		public long UserId { get; set; }
		public long RoleId { get; set; }
		public long VendorId { get; set; }

		public string FirstName { get; set; } = null!;

		public string? MiddleName { get; set; }

		public string? LastName { get; set; }

		public string? Address { get; set; }

		public long CityId { get; set; }

		public long StateId { get; set; }

		public long CountryId { get; set; }

		public string? Email { get; set; }

		public string? ContactNo { get; set; }

		public string? Contact_PersonName { get; set; }
		public string? Contact_PersonNo { get; set; }

		[NotMapped] public string ProjectIds { get; set; }
		[NotMapped] public string Projects { get; set; }
		[NotMapped] public string UserName { get; set; }
		[NotMapped] public string Password { get; set; }
		[NotMapped] public bool IsPassword_Reset { get; set; }

		[NotMapped] public string Fullname { get { return (string.IsNullOrEmpty(FirstName) ? "" : FirstName.Trim()) + (string.IsNullOrEmpty(MiddleName) ? "" : " " + MiddleName.Trim()) + (string.IsNullOrEmpty(LastName) ? "" : " " + LastName.Trim()); } }
	}

}
