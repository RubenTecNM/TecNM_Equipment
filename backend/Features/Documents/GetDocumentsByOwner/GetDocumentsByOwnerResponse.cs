namespace TecnmInventario.Api.Features.Documents.GetDocumentsByOwner;

public sealed record GetDocumentsByOwnerResponse(
    long DocumentId,
    string FileName,
    string MimeType,
    long SizeBytes,
    string? Description,
    DateTime CreatedAt,
    long? CreatedBy,
    int Version);
