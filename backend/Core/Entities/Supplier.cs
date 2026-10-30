using System;
using System.Collections.Generic;

namespace TecnmInventario.Api.Core.Entities;

public partial class Supplier
{
    public long SupplierId { get; set; }

    public string Name { get; set; } = null!;

    public string? TaxId { get; set; }

    public string? ContactName { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string? Address { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public long? DeletedBy { get; set; }

    public int Version { get; set; }

    public virtual ICollection<Acquisition> Acquisitions { get; set; } = new List<Acquisition>();

    public virtual ICollection<AssetWarranty> AssetWarranties { get; set; } = new List<AssetWarranty>();

    public virtual AppUser? CreatedByNavigation { get; set; }

    public virtual AppUser? DeletedByNavigation { get; set; }

    public virtual ICollection<SoftwareLicense> SoftwareLicenses { get; set; } = new List<SoftwareLicense>();

    public virtual AppUser? UpdatedByNavigation { get; set; }
}
