using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Core.Entities;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Assets.Shared;

namespace TecnmInventario.Api.Features.Assets.Assignments.CreateAssetAssignment;

public sealed class CreateAssetAssignmentHandler
    : ICommandHandler<CreateAssetAssignmentCommand, CreateAssetAssignmentResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;
    private readonly IAssetStatusChanger _statusChanger;

    public CreateAssetAssignmentHandler(
        TecnmInventarioDbContext context, ICurrentUserService currentUser,
        IAuditLogService auditLog, IAssetStatusChanger statusChanger)
    {
        _context = context;
        _currentUser = currentUser;
        _auditLog = auditLog;
        _statusChanger = statusChanger;
    }

    public async ValueTask<CreateAssetAssignmentResponse> Handle(
        CreateAssetAssignmentCommand command, CancellationToken cancellationToken)
    {
        var asset = await _context.Assets
            .Include(a => a.AssetStatus)
            .FirstOrDefaultAsync(
                a => a.AssetId == command.AssetId && !a.IsDeleted, cancellationToken);

        if (asset is null)
        {
            throw new KeyNotFoundException("El activo solicitado no existe.");
        }

        // Concurrencia optimista: la versión que el cliente vio debe
        // coincidir con la actual ANTES de abrir la transacción; así un
        // 409 no deja filas parciales ni transacción abierta.
        if (asset.Version != command.ExpectedVersion)
        {
            throw new DbUpdateConcurrencyException(
                "El activo fue modificado por otro usuario mientras tanto.");
        }

        // Máquina de estados: solo un activo DISPONIBLE puede asignarse.
        if (asset.AssetStatus.Code != AssetStatusCodes.Disponible)
        {
            throw new BusinessRuleViolationException(
                $"Solo se puede asignar un activo {AssetStatusCodes.Disponible}. " +
                $"Estado actual: {asset.AssetStatus.Code}.");
        }

        var hasActiveAssignment = await _context.AssetAssignments
            .AnyAsync(
                a => a.AssetId == command.AssetId && a.ReturnedAt == null,
                cancellationToken);

        if (hasActiveAssignment)
        {
            throw new BusinessRuleViolationException(
                "El activo ya tiene una asignación activa. Debe devolverse antes de asignarlo.");
        }

        var now = DateTime.UtcNow;

        var assignment = new AssetAssignment
        {
            AssetId = asset.AssetId,
            AssignedTo = command.AssignedTo,
            AssignmentTypeId = command.AssignmentTypeId,
            LocationId = command.LocationId,
            AssignedAt = command.AssignedAt ?? now,
            Notes = command.Notes,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = _currentUser.UserId,
            Version = 1
        };

        // Transacción explícita: la asignación, el paso del activo a ASIGNADO
        // y el rastro en audit_log son atómicos (todo o nada).
        // IAssetStatusChanger exige una transacción activa del llamador y él
        // mismo registra el cambio de estado en audit_log.
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        _context.AssetAssignments.Add(assignment);

        await _statusChanger.ChangeAsync(
            asset, AssetStatusCodes.Asignado, "Asignación de resguardo", cancellationToken);

        await _auditLog.LogAsync(
            tableName: "asset_assignments",
            recordId: assignment.AssetAssignmentId,
            action: "INSERT",
            oldData: null,
            newData: new
            {
                assignment.AssetId,
                assignment.AssignedTo,
                assignment.AssignmentTypeId,
                assignment.LocationId,
                assignment.AssignedAt,
            },
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        // El trigger touch_version subió version en la BD: releer con
        // AsNoTracking para devolver la versión nueva (el contexto
        // trackeado conserva la de carga).
        var assetVersion = await _context.Assets
            .AsNoTracking()
            .Where(a => a.AssetId == asset.AssetId)
            .Select(a => a.Version)
            .FirstAsync(cancellationToken);

        return new CreateAssetAssignmentResponse(
            assignment.AssetAssignmentId,
            assignment.AssetId,
            assignment.AssignedTo,
            assignment.AssignedAt,
            assetVersion);
    }
}
