using System;
using System.Collections.Generic;

namespace TecnmInventario.Api.Core.Entities;

public partial class SoftwareLicenseAssignment
{
    public long LicenseAssignmentId { get; set; }

    public long LicenseId { get; set; }

    public long AssetId { get; set; }

    public DateTime AssignedAt { get; set; }

    public DateTime? EndedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public int Version { get; set; }

    public virtual Asset Asset { get; set; } = null!;

    public virtual AppUser? CreatedByNavigation { get; set; }

    public virtual SoftwareLicense License { get; set; } = null!;

    public virtual AppUser? UpdatedByNavigation { get; set; }
}
