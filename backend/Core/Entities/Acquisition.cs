using System;
using System.Collections.Generic;

namespace TecnmInventario.Api.Core.Entities;

public partial class Acquisition
{
    public long AcquisitionId { get; set; }

    public long? SupplierId { get; set; }

    public string? PurchaseOrder { get; set; }

    public string? InvoiceNumber { get; set; }

    public DateOnly? AcquiredAt { get; set; }

    public decimal? Subtotal { get; set; }

    public decimal? TaxAmount { get; set; }

    public decimal? TotalAmount { get; set; }

    public string CurrencyCode { get; set; } = null!;

    public DateOnly? WarrantyStart { get; set; }

    public DateOnly? WarrantyEnd { get; set; }

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

    public virtual AppUser? CreatedByNavigation { get; set; }

    public virtual AppUser? DeletedByNavigation { get; set; }

    public virtual Supplier? Supplier { get; set; }

    public virtual AppUser? UpdatedByNavigation { get; set; }
}
