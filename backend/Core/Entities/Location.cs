using System;
using System.Collections.Generic;

namespace TecnmInventario.Api.Core.Entities;

public partial class Location
{
    public long LocationId { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public long LocationTypeId { get; set; }

    public long? ParentLocationId { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public long? DeletedBy { get; set; }

    public int Version { get; set; }

    public virtual ICollection<AssetAssignment> AssetAssignments { get; set; } = new List<AssetAssignment>();

    public virtual ICollection<AssetMovement> AssetMovementFromLocations { get; set; } = new List<AssetMovement>();

    public virtual ICollection<AssetMovement> AssetMovementToLocations { get; set; } = new List<AssetMovement>();

    public virtual ICollection<Asset> Assets { get; set; } = new List<Asset>();

    public virtual ICollection<BuildingSetting> BuildingSettings { get; set; } = new List<BuildingSetting>();

    public virtual AppUser? CreatedByNavigation { get; set; }

    public virtual AppUser? DeletedByNavigation { get; set; }

    public virtual ICollection<InventoryRecord> InventoryRecords { get; set; } = new List<InventoryRecord>();

    public virtual ICollection<Location> InverseParentLocation { get; set; } = new List<Location>();

    public virtual LocationType LocationType { get; set; } = null!;

    public virtual Location? ParentLocation { get; set; }

    public virtual AppUser? UpdatedByNavigation { get; set; }
}
