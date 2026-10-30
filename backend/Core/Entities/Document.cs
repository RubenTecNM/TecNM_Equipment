using System;

namespace TecnmInventario.Api.Core.Entities;

public partial class Document
{
    public long DocumentId { get; set; }

    public string OwnerType { get; set; } = null!;

    public long OwnerId { get; set; }

    public string FileName { get; set; } = null!;

    public string StorageKey { get; set; } = null!;

    public string MimeType { get; set; } = null!;

    public long SizeBytes { get; set; }

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public long? DeletedBy { get; set; }

    public int Version { get; set; }
}
