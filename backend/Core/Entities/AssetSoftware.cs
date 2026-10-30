using System;
using System.Collections.Generic;

namespace TecnmInventario.Api.Core.Entities;

/// <summary>
/// Historial de software instalado por activo.
/// </summary>
public partial class AssetSoftware
{
    public long AssetSoftwareId { get; set; }

    public long AssetId { get; set; }

    public long SoftwareId { get; set; }

    public string? VersionInstalled { get; set; }

    public DateTime InstalledAt { get; set; }

    public bool IsCurrent { get; set; }

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

    public virtual Software Software { get; set; } = null!;

    public virtual AppUser? UpdatedByNavigation { get; set; }
}
