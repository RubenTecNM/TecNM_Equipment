using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Behaviors;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Storage;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Core.Entities;

namespace TecnmInventario.Api.Features.Documents.UploadDocument;

public sealed class UploadDocumentHandler : ICommandHandler<UploadDocumentCommand, UploadDocumentResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly IFileStorageService _fileStorage;
    private readonly IDocumentAccessAuthorizer _accessAuthorizer;
    private readonly Common.Security.ICurrentUserService _currentUser;
    private readonly ITransactionHooks _hooks;
    private readonly IAuditLogService _auditLog;

    public UploadDocumentHandler(
        TecnmInventarioDbContext context,
        IFileStorageService fileStorage,
        IDocumentAccessAuthorizer accessAuthorizer,
        Common.Security.ICurrentUserService currentUser,
        ITransactionHooks hooks,
        IAuditLogService auditLog)
    {
        _context = context;
        _fileStorage = fileStorage;
        _accessAuthorizer = accessAuthorizer;
        _currentUser = currentUser;
        _hooks = hooks;
        _auditLog = auditLog;
    }

    public async ValueTask<UploadDocumentResponse> Handle(
        UploadDocumentCommand command, CancellationToken cancellationToken)
    {
        await _accessAuthorizer.EnsureAuthorizedAsync(command.OwnerType, cancellationToken);

        // ST5: el dueño debe existir y no estar borrado lógicamente. Va ANTES de
        // escribir el archivo: así un dueño inválido no deja nada en disco.
        await EnsureOwnerExistsAsync(command.OwnerType, command.OwnerId, cancellationToken);

        // ST4: el tipo sale del contenido. El validador ya lo comprobó; se repite
        // aquí porque el handler no debe fiarse de haberse invocado tras él.
        var fileType = DocumentFileTypes.Detect(command.File)
            ?? throw new BusinessRuleViolationException(
                $"Tipo de archivo no permitido. Formatos aceptados: {DocumentFileTypes.AcceptedFormatsDescription}.");

        // Nombre en disco: GUID + extensión del tipo DETECTADO.
        string storageKey;
        await using (var stream = command.File.OpenReadStream())
        {
            storageKey = await _fileStorage.SaveAsync(stream, fileType.Extension, cancellationToken);
        }

        // El archivo físico ya existe fuera de la base de datos: si la
        // transacción del comando no se confirma (fallo del SaveChanges o de
        // audit_log), se borra para no dejar huérfanos. El hook solo se
        // ejecuta cuando TransactionBehavior es el dueño de la transacción.
        _hooks.OnRollback(() => _fileStorage.DeleteAsync(storageKey, CancellationToken.None));

        var now = DateTime.UtcNow;
        var document = new Document
        {
            OwnerType = command.OwnerType,
            OwnerId = command.OwnerId,
            // El nombre del cliente es solo un dato (sin separadores ni controles).
            FileName = DocumentFileNames.Sanitize(command.File.FileName, fileType.Extension),
            StorageKey = storageKey,
            MimeType = fileType.MimeType,
            SizeBytes = command.File.Length,
            Description = command.Description,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = _currentUser.UserId,
            IsDeleted = false,
            Version = 1
        };

        _context.Documents.Add(document);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Red de seguridad si el handler se ejecuta sin el behavior (por
            // ejemplo, invocado directamente): DeleteAsync es idempotente.
            await _fileStorage.DeleteAsync(storageKey, CancellationToken.None);
            throw;
        }

        // StorageKey es una clave relativa (Guid + extensión), no una ruta
        // absoluta del servidor: se incluye en el snapshot sin exponer rutas.
        await _auditLog.LogAsync(
            tableName: "documents",
            recordId: document.DocumentId,
            action: "INSERT",
            oldData: null,
            newData: new
            {
                document.OwnerType,
                document.OwnerId,
                document.FileName,
                document.StorageKey,
                document.MimeType,
                document.SizeBytes,
                document.Description,
                document.Version
            },
            cancellationToken);

        return new UploadDocumentResponse(
            document.DocumentId, document.OwnerType, document.OwnerId,
            document.FileName, document.MimeType, document.SizeBytes);
    }

    // Resolución del dueño por OwnerType (los mismos 4 tipos que el CHECK de
    // documents.owner_type y que el trigger trg_documents_validate_owner de la
    // BD, que solo comprueba EXISTENCIA, no el borrado lógico).
    // AssetDisposal no tiene is_deleted: una baja formal no se borra.
    // R3c (regla 20): un incidente o un mantenimiento cuyo ACTIVO está borrado
    // lógicamente tampoco admite documentos. Acquisition no tiene un activo
    // directo (relación N:M en acquisition_assets) y AssetDisposal sí, pero su
    // regla con el borrado del activo está pendiente del módulo AssetDisposal.
    private async Task EnsureOwnerExistsAsync(string ownerType, long ownerId, CancellationToken cancellationToken)
    {
        var exists = ownerType switch
        {
            DocumentOwnerTypes.Incident => await _context.Incidents
                .AnyAsync(i => i.IncidentId == ownerId && !i.IsDeleted
                    && _context.Assets.Any(a => a.AssetId == i.AssetId && !a.IsDeleted), cancellationToken),
            DocumentOwnerTypes.Maintenance => await _context.MaintenanceRecords
                .AnyAsync(m => m.MaintenanceRecordId == ownerId && !m.IsDeleted
                    && _context.Assets.Any(a => a.AssetId == m.AssetId && !a.IsDeleted), cancellationToken),
            DocumentOwnerTypes.AssetDisposal => await _context.AssetDisposals
                .AnyAsync(d => d.AssetDisposalId == ownerId, cancellationToken),
            DocumentOwnerTypes.Acquisition => await _context.Acquisitions
                .AnyAsync(a => a.AcquisitionId == ownerId && !a.IsDeleted, cancellationToken),
            _ => false
        };

        if (!exists)
        {
            throw new BusinessRuleViolationException(
                "El elemento al que se quiere adjuntar el documento no existe o fue eliminado.");
        }
    }
}
