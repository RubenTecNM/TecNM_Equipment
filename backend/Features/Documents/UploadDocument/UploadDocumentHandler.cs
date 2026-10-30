using Mediator;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Behaviors;
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

        string storageKey;
        await using (var stream = command.File.OpenReadStream())
        {
            storageKey = await _fileStorage.SaveAsync(stream, command.File.FileName, cancellationToken);
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
            FileName = command.File.FileName,
            StorageKey = storageKey,
            MimeType = command.File.ContentType,
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
}
