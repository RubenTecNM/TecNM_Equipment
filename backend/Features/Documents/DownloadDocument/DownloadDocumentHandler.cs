using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Storage;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Documents.DownloadDocument;

public sealed class DownloadDocumentHandler : IQueryHandler<DownloadDocumentQuery, DownloadDocumentResult>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly IFileStorageService _fileStorage;
    private readonly IDocumentAccessAuthorizer _accessAuthorizer;

    public DownloadDocumentHandler(
        TecnmInventarioDbContext context,
        IFileStorageService fileStorage,
        IDocumentAccessAuthorizer accessAuthorizer)
    {
        _context = context;
        _fileStorage = fileStorage;
        _accessAuthorizer = accessAuthorizer;
    }

    public async ValueTask<DownloadDocumentResult> Handle(
        DownloadDocumentQuery query, CancellationToken cancellationToken)
    {
        var document = await _context.Documents
            .FirstOrDefaultAsync(d => d.DocumentId == query.DocumentId && !d.IsDeleted, cancellationToken);

        if (document is null)
        {
            throw new KeyNotFoundException("El documento solicitado no existe.");
        }

        await _accessAuthorizer.EnsureAuthorizedAsync(document.OwnerType, cancellationToken);

        var stream = await _fileStorage.OpenReadAsync(document.StorageKey, cancellationToken);

        return new DownloadDocumentResult(stream, document.FileName, document.MimeType);
    }
}
