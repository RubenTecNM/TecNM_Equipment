using System;
using System.Collections.Generic;

namespace TecnmInventario.Api.Core.Entities;

public partial class MaintenanceRecord
{
    public long MaintenanceRecordId { get; set; }

    public long AssetId { get; set; }

    public long MaintenanceTypeId { get; set; }

    public long? PerformedBy { get; set; }

    public DateTime? ScheduledAt { get; set; }

    public DateTime? PerformedAt { get; set; }

    public DateTime? NextMaintenanceAt { get; set; }

    public string? Description { get; set; }

    public string? PartsUsed { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public long? DeletedBy { get; set; }

    public int Version { get; set; }

    public virtual Asset Asset { get; set; } = null!;

    public virtual AppUser? CreatedByNavigation { get; set; }

    public virtual AppUser? DeletedByNavigation { get; set; }

    public virtual MaintenanceType MaintenanceType { get; set; } = null!;

    public virtual AppUser? PerformedByNavigation { get; set; }

    public virtual AppUser? UpdatedByNavigation { get; set; }
}
