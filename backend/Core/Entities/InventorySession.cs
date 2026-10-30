using System;
using System.Collections.Generic;

namespace TecnmInventario.Api.Core.Entities;

public partial class InventorySession
{
    public long InventorySessionId { get; set; }

    public string Name { get; set; } = null!;

    public DateTime StartedAt { get; set; }

    public DateTime? EndedAt { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public int Version { get; set; }

    public virtual AppUser? CreatedByNavigation { get; set; }

    public virtual ICollection<InventoryRecord> InventoryRecords { get; set; } = new List<InventoryRecord>();

    public virtual AppUser? UpdatedByNavigation { get; set; }
}
