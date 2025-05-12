using System;
using System.Collections.Generic;

namespace Leoz_25.Models_Temp;

public partial class Employee
{
    public long Id { get; set; }

    public long UserId { get; set; }

    public long RoleId { get; set; }

    public long VendorId { get; set; }

    public string FirstName { get; set; } = null!;

    public string LastName { get; set; } = null!;

    public string? MiddleName { get; set; }

    public string? UserType { get; set; }

    public string? Address { get; set; }

    public long? CityId { get; set; }

    public long? StateId { get; set; }

    public long? CountryId { get; set; }

    public string? Gender { get; set; }

    public string? Position { get; set; }

    public string? ContactNo { get; set; }

    public string? BloodGroup { get; set; }

    public DateTime? BirthDate { get; set; }

    public DateTime? HireDate { get; set; }

    public long CreatedBy { get; set; }

    public DateTime? CreatedDate { get; set; }

    public long LastModifiedBy { get; set; }

    public DateTime? LastModifiedDate { get; set; }

    public bool IsActive { get; set; }

    public bool IsDeleted { get; set; }
}
