using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Assets.UpdateAsset;

public sealed class UpdateAssetHandler : ICommandHandler<UpdateAssetCommand, UpdateAssetResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;

    public UpdateAssetHandler(
        TecnmInventarioDbContext context,
        ICurrentUserService currentUser,
        IAuditLogService auditLog)
    {
        _context = context;
        _currentUser = currentUser;
        _auditLog = auditLog;
    }

    public async ValueTask<UpdateAssetResponse> Handle(
        UpdateAssetCommand command, CancellationToken cancellationToken)
    {
        // Asset no tiene query filter global (Lote 4): el filtro por IsDeleted
        // se hace explícito para no editar registros ya dados de baja lógica.
        var asset = await _context.Assets
            .Include(a => a.AssetStatus)
            .FirstOrDefaultAsync(
                a => a.AssetId == command.AssetId && !a.IsDeleted,
                cancellationToken);

        if (asset is null)
        {
            throw new KeyNotFoundException("El activo solicitado no existe.");
        }

        // Concurrencia optimista: la versión que vio el cliente debe coincidir
        // con la actual, incluso si los datos enviados son idénticos a los
        // almacenados. Se reutiliza el mecanismo ya mapeado a 409 en el
        // middleware (DbUpdateConcurrencyException), igual que en movimientos.
        if (asset.Version != command.ExpectedVersion)
        {
            throw new DbUpdateConcurrencyException(
                "El activo fue modificado por otro usuario mientras tanto.");
        }

        if (asset.AssetStatus.Code == AssetStatusCodes.DadoDeBaja)
        {
            throw new BusinessRuleViolationException(
                "El activo está dado de baja. Ese estado es terminal y el activo ya no puede editarse.");
        }

        var serialNumber = Normalize(command.SerialNumber);
        var hostname = Normalize(command.Hostname);

        // Un PUT idéntico no debe generar UPDATE, ni subir Version, ni dejar
        // fila en audit_log. Se compara el estado normalizado antes de mutar.
        var current = (
            asset.ManufacturerId,
            asset.ModelId,
            Normalize(asset.SerialNumber),
            Normalize(asset.Hostname),
            asset.Notes);
        var requested = (
            command.ManufacturerId,
            command.ModelId,
            serialNumber,
            hostname,
            command.Notes);

        if (current == requested)
        {
            return new UpdateAssetResponse(asset.AssetId, asset.Version);
        }

        // Snapshot "antes" ANTES de mutar la entidad.
        var before = new
        {
            asset.ManufacturerId,
            asset.ModelId,
            asset.SerialNumber,
            asset.Hostname,
            asset.Notes,
            asset.Version
        };

        _context.Entry(asset).Property(a => a.Version).OriginalValue = command.ExpectedVersion;

        // La ubicación NO se edita aquí: solo cambia con asset_movements.
        asset.ManufacturerId = command.ManufacturerId;
        asset.ModelId = command.ModelId;
        asset.SerialNumber = serialNumber;
        asset.Hostname = hostname;
        asset.Notes = command.Notes;
        asset.UpdatedBy = _currentUser.UserId;

        await _context.SaveChangesAsync(cancellationToken);
        await _context.Entry(asset).ReloadAsync(cancellationToken);

        // Después del ReloadAsync para que el snapshot lleve la Version real
        // que dejó el trigger touch_version.
        var after = new
        {
            asset.ManufacturerId,
            asset.ModelId,
            asset.SerialNumber,
            asset.Hostname,
            asset.Notes,
            asset.Version
        };

        await _auditLog.LogAsync(
            tableName: "assets",
            recordId: asset.AssetId,
            action: "UPDATE",
            oldData: before,
            newData: after,
            cancellationToken);

        return new UpdateAssetResponse(asset.AssetId, asset.Version);
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
