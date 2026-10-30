using System;
using System.Collections.Generic;

namespace TecnmInventario.Api.Core.Entities;

public partial class BuildingSetting
{
    public long BuildingSettingId { get; set; }

    public long LocationId { get; set; }

    public string SettingKey { get; set; } = null!;

    public string? SettingValue { get; set; }

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

    public virtual Location Location { get; set; } = null!;

    public virtual AppUser? UpdatedByNavigation { get; set; }
}
