using System;
using System.Collections.Generic;

namespace TecnmInventario.Api.Core.Entities;

public partial class AssetAssignment
{
    public long AssetAssignmentId { get; set; }

    public long AssetId { get; set; }

    public long AssignedTo { get; set; }

    public long AssignmentTypeId { get; set; }

    public long? LocationId { get; set; }

    public DateTime AssignedAt { get; set; }

    public DateTime? ReturnedAt { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public int Version { get; set; }

    public virtual Asset Asset { get; set; } = null!;

    public virtual AppUser AssignedToNavigation { get; set; } = null!;

    public virtual AssignmentType AssignmentType { get; set; } = null!;

    public virtual AppUser? CreatedByNavigation { get; set; }

    public virtual Location? Location { get; set; }

    public virtual AppUser? UpdatedByNavigation { get; set; }
}
