using System;
using System.Collections.Generic;

namespace TecnmInventario.Api.Core.Entities;

/// <summary>
/// Identidad administrativa de cada equipo físico inventariable. No se elimina físicamente.
/// </summary>
public partial class Asset
{
    public long AssetId { get; set; }

    /// <summary>
    /// Código de inventario estable e independiente de la ubicación (ej. TEC-PC-0001).
    /// </summary>
    public string InventoryCode { get; set; } = null!;

    public long AssetTypeId { get; set; }

    public long AssetStatusId { get; set; }

    public long? ManufacturerId { get; set; }

    public long? ModelId { get; set; }

    /// <summary>
    /// Ubicación actual para consulta rápida; los cambios históricos están en asset_movements.
    /// </summary>
    public long? CurrentLocationId { get; set; }

    public string? SerialNumber { get; set; }

    public string? Hostname { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public long? DeletedBy { get; set; }

    public int Version { get; set; }

    public virtual ICollection<AcquisitionAsset> AcquisitionAssets { get; set; } = new List<AcquisitionAsset>();

    public virtual ICollection<AssetAssignment> AssetAssignments { get; set; } = new List<AssetAssignment>();

    public virtual ICollection<AssetComponent> AssetComponents { get; set; } = new List<AssetComponent>();

    public virtual AssetDisposal? AssetDisposal { get; set; }

    public virtual ICollection<AssetMovement> AssetMovements { get; set; } = new List<AssetMovement>();

    public virtual AssetOsHistory? AssetOsHistory { get; set; }

    public virtual ICollection<AssetRelation> AssetRelationPrimaryAssets { get; set; } = new List<AssetRelation>();

    public virtual ICollection<AssetRelation> AssetRelationRelatedAssets { get; set; } = new List<AssetRelation>();

    public virtual ICollection<AssetService> AssetServices { get; set; } = new List<AssetService>();

    public virtual ICollection<AssetSoftware> AssetSoftwares { get; set; } = new List<AssetSoftware>();

    public virtual ICollection<AssetSpecification> AssetSpecifications { get; set; } = new List<AssetSpecification>();

    public virtual AssetStatus AssetStatus { get; set; } = null!;

    public virtual AssetType AssetType { get; set; } = null!;

    public virtual ICollection<AssetWarranty> AssetWarranties { get; set; } = new List<AssetWarranty>();

    public virtual AppUser? CreatedByNavigation { get; set; }

    public virtual Location? CurrentLocation { get; set; }

    public virtual AppUser? DeletedByNavigation { get; set; }

    public virtual ICollection<Incident> Incidents { get; set; } = new List<Incident>();

    public virtual ICollection<InventoryRecord> InventoryRecords { get; set; } = new List<InventoryRecord>();

    public virtual ICollection<MaintenanceRecord> MaintenanceRecords { get; set; } = new List<MaintenanceRecord>();

    public virtual Manufacturer? Manufacturer { get; set; }

    public virtual Model? Model { get; set; }

    public virtual ICollection<NetworkInterface> NetworkInterfaces { get; set; } = new List<NetworkInterface>();

    public virtual ICollection<SoftwareLicenseAssignment> SoftwareLicenseAssignments { get; set; } = new List<SoftwareLicenseAssignment>();

    public virtual AppUser? UpdatedByNavigation { get; set; }
}
