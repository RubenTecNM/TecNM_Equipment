using System;
using System.Collections.Generic;

namespace TecnmInventario.Api.Core.Entities;

public partial class Model
{
    public long ModelId { get; set; }

    public long? ManufacturerId { get; set; }

    public long? AssetTypeId { get; set; }

    public string ModelName { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public long? DeletedBy { get; set; }

    public int Version { get; set; }

    public virtual AssetType? AssetType { get; set; }

    public virtual ICollection<Asset> Assets { get; set; } = new List<Asset>();

    public virtual AppUser? CreatedByNavigation { get; set; }

    public virtual AppUser? DeletedByNavigation { get; set; }

    public virtual Manufacturer? Manufacturer { get; set; }

    public virtual AppUser? UpdatedByNavigation { get; set; }
}
