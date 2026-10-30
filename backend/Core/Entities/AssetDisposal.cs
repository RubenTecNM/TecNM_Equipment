using System;
using System.Collections.Generic;

namespace TecnmInventario.Api.Core.Entities;

/// <summary>
/// Baja administrativa; el asset original permanece en assets.
/// </summary>
public partial class AssetDisposal
{
    public long AssetDisposalId { get; set; }

    public long AssetId { get; set; }

    public long DisposalReasonId { get; set; }

    public string Folio { get; set; } = null!;

    public string? Dictamen { get; set; }

    public string? ActDocumentPath { get; set; }

    public DateOnly DisposedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public int Version { get; set; }

    public virtual Asset Asset { get; set; } = null!;

    public virtual AppUser? CreatedByNavigation { get; set; }

    public virtual DisposalReason DisposalReason { get; set; } = null!;

    public virtual AppUser? UpdatedByNavigation { get; set; }
}
