using System;
using System.Collections.Generic;

namespace TecnmInventario.Api.Core.Entities;

/// <summary>
/// Componentes internos de un activo; conserva instalación y retiro para historial.
/// </summary>
public partial class AssetComponent
{
    public long AssetComponentId { get; set; }

    public long AssetId { get; set; }

    public long ComponentTypeId { get; set; }

    public string? Description { get; set; }

    public string? SerialNumber { get; set; }

    public DateTime InstalledAt { get; set; }

    public DateTime? RemovedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public long? DeletedBy { get; set; }

    public int Version { get; set; }

    public virtual Asset Asset { get; set; } = null!;

    public virtual ComponentType ComponentType { get; set; } = null!;

    public virtual AppUser? CreatedByNavigation { get; set; }

    public virtual AppUser? DeletedByNavigation { get; set; }

    public virtual AppUser? UpdatedByNavigation { get; set; }
}
