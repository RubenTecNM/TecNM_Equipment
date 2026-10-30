using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Deletion;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Documents.DeleteDocument;

public sealed class DeleteDocumentHandler : ICommandHandler<DeleteDocumentCommand, DeleteDocumentResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly IDocumentAccessAuthorizer _accessAuthorizer;
    private readonly ISoftDeleteAuditor _softDeleteAuditor;

    public DeleteDocumentHandler(
        TecnmInventarioDbContext context,
        IDocumentAccessAuthorizer accessAuthorizer,
        ISoftDeleteAuditor softDeleteAuditor)
    {
        _context = context;
        _accessAuthorizer = accessAuthorizer;
        _softDeleteAuditor = softDeleteAuditor;
    }

    public async ValueTask<DeleteDocumentResponse> Handle(
        DeleteDocumentCommand command, CancellationToken cancellationToken)
    {
        var document = await _context.Documents
            .FirstOrDefaultAsync(d => d.DocumentId == command.DocumentId, cancellationToken);

        if (document is null)
        {
            throw new KeyNotFoundException("El documento solicitado no existe.");
        }

        await _accessAuthorizer.EnsureAuthorizedAsync(document.OwnerType, cancellationToken);

        // Snapshot ANTES de borrar: la auditoría debe conservar exactamente
        // qué se eliminó (quién lo subió, cuándo, con qué se relacionaba).
        var beforeSnapshot = new
        {
            document.DocumentId,
            document.OwnerType,
            document.OwnerId,
            document.FileName,
            document.MimeType,
            document.SizeBytes,
            document.Description,
            document.CreatedAt,
            document.CreatedBy,
            document.Version
        };

        await _softDeleteAuditor.DeleteAsync(
            document,
            recordId: document.DocumentId,
            expectedVersion: command.ExpectedVersion,
            tableName: "documents",
            snapshotBeforeDelete: beforeSnapshot,
            cancellationToken);

        return new DeleteDocumentResponse(document.DocumentId, true);
    }
}
