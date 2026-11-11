using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TecnmInventario.Api.Common.Storage;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Documents.DownloadDocument;

public sealed class DownloadDocumentHandler : IQueryHandler<DownloadDocumentQuery, DownloadDocumentResult>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly IFileStorageService _fileStorage;
    private readonly IDocumentAccessAuthorizer _accessAuthorizer;
    private readonly ILogger<DownloadDocumentHandler> _logger;

    public DownloadDocumentHandler(
        TecnmInventarioDbContext context,
        IFileStorageService fileStorage,
        IDocumentAccessAuthorizer accessAuthorizer,
        ILogger<DownloadDocumentHandler> logger)
    {
        _context = context;
        _fileStorage = fileStorage;
        _accessAuthorizer = accessAuthorizer;
        _logger = logger;
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

        // ST2: registro existente pero archivo físico ausente, o clave que no
        // resuelve dentro de la raíz (traversal, enlace simbólico): 404 con
        // mensaje fijo. El log lleva el document_id y el tipo de fallo, NUNCA
        // rutas absolutas ni la clave de almacenamiento.
        Stream stream;
        try
        {
            stream = await _fileStorage.OpenReadAsync(document.StorageKey, cancellationToken);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException or StoragePathException)
        {
            _logger.LogError(
                "Descarga imposible para el documento {DocumentId}: {Reason}",
                document.DocumentId, ex.GetType().Name);
            throw new KeyNotFoundException("El archivo no está disponible.");
        }

        // R3b: el nombre de Content-Disposition pasa SIEMPRE por la misma
        // sanitización que en la subida, también para documentos antiguos cuyo
        // file_name se guardó crudo (con separadores o ".."). La extensión de
        // respaldo sale de la clave de almacenamiento (GUID + extensión).
        var downloadName = DocumentFileNames.Sanitize(document.FileName, Path.GetExtension(document.StorageKey));

        return new DownloadDocumentResult(stream, downloadName, document.MimeType);
    }
}
