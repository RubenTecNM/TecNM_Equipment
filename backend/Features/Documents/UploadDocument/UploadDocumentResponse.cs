namespace TecnmInventario.Api.Features.Documents.UploadDocument;

public sealed record UploadDocumentResponse(
    long DocumentId,
    string OwnerType,
    long OwnerId,
    string FileName,
    string MimeType,
    long SizeBytes);
