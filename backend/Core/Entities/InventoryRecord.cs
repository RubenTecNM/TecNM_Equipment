using System;
using System.Collections.Generic;

namespace TecnmInventario.Api.Core.Entities;

/// <summary>
/// Resultado de una verificación física contra la información de la BD.
/// </summary>
public partial class InventoryRecord
{
    public long InventoryRecordId { get; set; }

    public long InventorySessionId { get; set; }

    public long AssetId { get; set; }

    public bool Found { get; set; }

    public long? LocationFoundId { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public int Version { get; set; }

    public virtual Asset Asset { get; set; } = null!;

    public virtual AppUser? CreatedByNavigation { get; set; }

    public virtual InventorySession InventorySession { get; set; } = null!;

    public virtual Location? LocationFound { get; set; }

    public virtual AppUser? UpdatedByNavigation { get; set; }
}
