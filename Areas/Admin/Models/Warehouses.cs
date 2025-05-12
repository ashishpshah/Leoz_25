using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Numerics;

namespace Leoz_25
{
	public partial class Warehouses : EntitiesBase
	{
		public long Id { get; set; }
		public string WarehouseName { get; set; }
		public string ContactPerson { get; set; }
		public string Email { get; set; }
		public string Address { get; set; }
		public string City { get; set; }
		public string State { get; set; }
		public int StateId { get; set; }
		public int City_Id { get; set; }
		public string Phone { get; set; }
		public string Pincode { get; set; }
		public string GSTNumber { get; set; }
		public string Capacity { get; set; }
		public string Status { get; set; }
        public string Name { get; set; }
    }
}
