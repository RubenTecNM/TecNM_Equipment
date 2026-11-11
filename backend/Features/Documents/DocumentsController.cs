using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TecnmInventario.Api.Features.Documents.DeleteDocument;
using TecnmInventario.Api.Features.Documents.DownloadDocument;
using TecnmInventario.Api.Features.Documents.GetDocumentsByOwner;
using TecnmInventario.Api.Features.Documents.UploadDocument;

namespace TecnmInventario.Api.Features.Documents;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DocumentsController : ControllerBase
{
    private readonly IMediator _mediator;

    public DocumentsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // Límite de la petición: 5 MB + 64 KB de margen para el resto del formulario
    // (DocumentUploadLimits). Si lo supera: 413 (DocumentUploadSizeLimit, por
    // Content-Length) y [RequestSizeLimit] como defensa sin Content-Length. El
    // máximo del archivo (Storage:MaxFileSizeBytes) lo valida el validador (400).
    [HttpPost]
    [DocumentUploadSizeLimit]
    [RequestSizeLimit(DocumentUploadLimits.MaxRequestBodyBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = DocumentUploadLimits.MaxRequestBodyBytes)]
    public async Task<ActionResult<UploadDocumentResponse>> UploadDocument(
        [FromForm] UploadDocumentRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UploadDocumentCommand(
            request.OwnerType, request.OwnerId, request.Description, request.File);

        var result = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(UploadDocument), new { result.DocumentId }, result);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<GetDocumentsByOwnerResponse>>> GetDocumentsByOwner(
        [FromQuery] string ownerType,
        [FromQuery] long ownerId,
        CancellationToken cancellationToken)
    {
        var query = new GetDocumentsByOwnerQuery(ownerType, ownerId);
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{documentId:long}/download")]
    public async Task<IActionResult> DownloadDocument(
        long documentId,
        CancellationToken cancellationToken)
    {
        var query = new DownloadDocumentQuery(documentId);
        var result = await _mediator.Send(query, cancellationToken);
        return File(result.Content, result.MimeType, result.FileName);
    }

    [HttpDelete("{documentId:long}")]
    public async Task<ActionResult<DeleteDocumentResponse>> DeleteDocument(
        long documentId,
        [FromQuery] int expectedVersion,
        CancellationToken cancellationToken)
    {
        var command = new DeleteDocumentCommand(documentId, expectedVersion);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }
}
