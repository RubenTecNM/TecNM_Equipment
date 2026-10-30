using System;
using System.Collections.Generic;

namespace TecnmInventario.Api.Core.Entities;

public partial class AssetWarranty
{
    public long WarrantyId { get; set; }

    public long AssetId { get; set; }

    public long? SupplierId { get; set; }

    public string? WarrantyNumber { get; set; }

    public DateOnly? StartsOn { get; set; }

    public DateOnly? EndsOn { get; set; }

    public string? Coverage { get; set; }

    public string? Terms { get; set; }

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

    public virtual Supplier? Supplier { get; set; }

    public virtual AppUser? UpdatedByNavigation { get; set; }
}
