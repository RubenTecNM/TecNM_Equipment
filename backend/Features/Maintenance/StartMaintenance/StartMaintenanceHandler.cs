using Mediator;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Assets.Shared;
using TecnmInventario.Api.Features.Maintenance.Shared;

namespace TecnmInventario.Api.Features.Maintenance.StartMaintenance;

public sealed class StartMaintenanceHandler : ICommandHandler<StartMaintenanceCommand, StartMaintenanceResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;
    private readonly IAssetStatusChanger _assetStatusChanger;

    public StartMaintenanceHandler(
        TecnmInventarioDbContext context,
        ICurrentUserService currentUser,
        IAuditLogService auditLog,
        IAssetStatusChanger assetStatusChanger)
    {
        _context = context;
        _currentUser = currentUser;
        _auditLog = auditLog;
        _assetStatusChanger = assetStatusChanger;
    }

    public async ValueTask<StartMaintenanceResponse> Handle(
        StartMaintenanceCommand command, CancellationToken cancellationToken)
    {
        // Solo se lee el id del activo (sin lock); el lock y todo lo demás ocurren
        // dentro de MaintenanceStartFlow, con el activo SIEMPRE primero.
        var assetId = await MaintenanceGuards.ReadAssetIdAsync(
            _context, command.MaintenanceRecordId, cancellationToken);

        var result = await MaintenanceStartFlow.ExecuteAsync(
            _context, _currentUser, _auditLog, _assetStatusChanger,
            new MaintenanceStartRequest(
                assetId, command.MaintenanceRecordId, command.ExpectedVersion,
                MaintenanceTypeId: null, PerformedBy: null, Description: null, command.IncidentId),
            cancellationToken);

        return new StartMaintenanceResponse(
            result.MaintenanceRecordId, MaintenanceStatusCodes.EnCurso,
            result.AssetId, result.AssetStatusCode, result.StartedAt, result.Version);
    }
}
