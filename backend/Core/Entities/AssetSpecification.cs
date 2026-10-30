using System;
using System.Collections.Generic;

namespace TecnmInventario.Api.Core.Entities;

public partial class AssetSpecification
{
    public long AssetSpecificationId { get; set; }

    public long AssetId { get; set; }

    public string SpecificationKey { get; set; } = null!;

    public string? SpecificationValue { get; set; }

    public string? Unit { get; set; }

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
