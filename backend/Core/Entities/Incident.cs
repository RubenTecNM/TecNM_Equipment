using System;
using System.Collections.Generic;

namespace TecnmInventario.Api.Core.Entities;

public partial class Incident
{
    public long IncidentId { get; set; }

    public long AssetId { get; set; }

    public long IncidentTypeId { get; set; }

    public long IncidentPriorityId { get; set; }

    public long IncidentStatusId { get; set; }

    public long ReportedBy { get; set; }

    public long? AssignedTo { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public string? Diagnosis { get; set; }

    public string? Solution { get; set; }

    public DateTime ReportedAt { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public long? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public long? DeletedBy { get; set; }

    public int Version { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Asset Asset { get; set; } = null!;

    public virtual AppUser? AssignedToNavigation { get; set; }

    public virtual AppUser? CreatedByNavigation { get; set; }

    public virtual AppUser? DeletedByNavigation { get; set; }

    public virtual IncidentPriority IncidentPriority { get; set; } = null!;

    public virtual IncidentStatus IncidentStatus { get; set; } = null!;

    public virtual IncidentType IncidentType { get; set; } = null!;

    public virtual AppUser ReportedByNavigation { get; set; } = null!;

    public virtual AppUser? UpdatedByNavigation { get; set; }
}
