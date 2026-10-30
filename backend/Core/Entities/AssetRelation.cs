using System;
using System.Collections.Generic;

namespace TecnmInventario.Api.Core.Entities;

public partial class AssetRelation
{
    public long AssetRelationId { get; set; }

    public long PrimaryAssetId { get; set; }

    public long RelatedAssetId { get; set; }

    public long RelationTypeId { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public int Version { get; set; }

    public virtual AppUser? CreatedByNavigation { get; set; }

    public virtual Asset PrimaryAsset { get; set; } = null!;

    public virtual Asset RelatedAsset { get; set; } = null!;

    public virtual RelationType RelationType { get; set; } = null!;

    public virtual AppUser? UpdatedByNavigation { get; set; }
}
