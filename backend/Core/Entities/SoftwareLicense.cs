using System;
using System.Collections.Generic;

namespace TecnmInventario.Api.Core.Entities;

public partial class SoftwareLicense
{
    public long LicenseId { get; set; }

    public long SoftwareId { get; set; }

    public long LicenseTypeId { get; set; }

    public string? LicenseKey { get; set; }

    public int? QuantityPurchased { get; set; }

    public DateOnly? ValidFrom { get; set; }

    public DateOnly? ValidUntil { get; set; }

    public long? SupplierId { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public long? DeletedBy { get; set; }

    public int Version { get; set; }

    public virtual AppUser? CreatedByNavigation { get; set; }

    public virtual AppUser? DeletedByNavigation { get; set; }

    public virtual LicenseType LicenseType { get; set; } = null!;

    public virtual Software Software { get; set; } = null!;

    public virtual ICollection<SoftwareLicenseAssignment> SoftwareLicenseAssignments { get; set; } = new List<SoftwareLicenseAssignment>();

    public virtual Supplier? Supplier { get; set; }

    public virtual AppUser? UpdatedByNavigation { get; set; }
}
