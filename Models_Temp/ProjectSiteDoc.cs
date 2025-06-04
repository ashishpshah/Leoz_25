using System;
using System.Collections.Generic;

namespace Leoz_25.Models_Temp;

public partial class ProjectSiteDoc
{
    public long Id { get; set; }

    public long ProjectId { get; set; }

    public long CustomerId { get; set; }

    public DateTime UploadDate { get; set; }

    public string? FilePath { get; set; }

    public string Remark { get; set; } = null!;

    public string Type { get; set; } = null!;

    public string Status { get; set; } = null!;

    public DateTime StatusDate { get; set; }

    public bool IsActive { get; set; }

    public bool IsDeleted { get; set; }

    public long CreatedBy { get; set; }

    public DateTime? CreatedDate { get; set; }

    public long LastModifiedBy { get; set; }

    public DateTime? LastModifiedDate { get; set; }
}
