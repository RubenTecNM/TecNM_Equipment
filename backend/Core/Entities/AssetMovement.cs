using System;
using System.Collections.Generic;

namespace TecnmInventario.Api.Core.Entities;

/// <summary>
/// Historial de movimientos. No debe borrarse para corregir ubicación.
/// </summary>
public partial class AssetMovement
{
    public long AssetMovementId { get; set; }

    public long AssetId { get; set; }

    public long? FromLocationId { get; set; }

    public long ToLocationId { get; set; }

    public long MovementReasonId { get; set; }

    public long MovedBy { get; set; }

    public DateTime MovedAt { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public int Version { get; set; }

    public virtual Asset Asset { get; set; } = null!;

    public virtual AppUser? CreatedByNavigation { get; set; }

    public virtual Location? FromLocation { get; set; }

    public virtual AppUser MovedByNavigation { get; set; } = null!;

    public virtual MovementReason MovementReason { get; set; } = null!;

    public virtual Location ToLocation { get; set; } = null!;

    public virtual AppUser? UpdatedByNavigation { get; set; }
}
