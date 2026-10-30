using System;
using System.Collections.Generic;

namespace TecnmInventario.Api.Core.Entities;

/// <summary>
/// Historial de sistemas operativos instalados por activo.
/// </summary>
public partial class AssetOsHistory
{
    public long AssetOsHistoryId { get; set; }

    public long AssetId { get; set; }

    public string OsName { get; set; } = null!;

    public string? OsVersion { get; set; }

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

    public virtual AppUser? UpdatedByNavigation { get; set; }
}
