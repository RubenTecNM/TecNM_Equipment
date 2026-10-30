using System;
using System.Collections.Generic;

namespace TecnmInventario.Api.Core.Entities;

public partial class SoftwareType
{
    public long SoftwareTypeId { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

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

    public virtual ICollection<Software> Softwares { get; set; } = new List<Software>();

    public virtual AppUser? UpdatedByNavigation { get; set; }
}
