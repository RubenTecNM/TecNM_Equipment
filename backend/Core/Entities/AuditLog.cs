using System;
using System.Collections.Generic;
using System.Net;

namespace TecnmInventario.Api.Core.Entities;

/// <summary>
/// Registro inmutable de auditoría. No debe modificarse ni eliminarse desde la aplicación.
/// </summary>
public partial class AuditLog
{
    public long AuditLogId { get; set; }

    public string TableName { get; set; } = null!;

    public long RecordId { get; set; }

    public string Action { get; set; } = null!;

    public string? OldData { get; set; }

    public string? NewData { get; set; }

    public long? PerformedBy { get; set; }

    public DateTime PerformedAt { get; set; }

    public IPAddress? ClientIp { get; set; }

    public virtual AppUser? PerformedByNavigation { get; set; }
}
