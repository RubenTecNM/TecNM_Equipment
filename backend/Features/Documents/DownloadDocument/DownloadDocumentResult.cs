namespace TecnmInventario.Api.Features.Documents.DownloadDocument;

/// <summary>
/// No es un DTO de salida JSON: transporta el stream abierto del archivo
/// hasta el controller, que lo convierte en un FileStreamResult.
/// </summary>
public sealed record DownloadDocumentResult(
    Stream Content,
    string FileName,
    string MimeType);
