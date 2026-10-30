using Microsoft.AspNetCore.Http;

namespace TecnmInventario.Api.Features.Documents.UploadDocument;

public sealed class UploadDocumentRequest
{
    public string OwnerType { get; set; } = null!;
    public long OwnerId { get; set; }
    public string? Description { get; set; }
    public IFormFile File { get; set; } = null!;
}
