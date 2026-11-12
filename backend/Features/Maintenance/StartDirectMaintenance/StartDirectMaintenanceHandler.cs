using Mediator;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Assets.Shared;
using TecnmInventario.Api.Features.Maintenance.Shared;

namespace TecnmInventario.Api.Features.Maintenance.StartDirectMaintenance;

public sealed class StartDirectMaintenanceHandler
    : ICommandHandler<StartDirectMaintenanceCommand, StartDirectMaintenanceResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;
    private readonly IAssetStatusChanger _assetStatusChanger;

    public StartDirectMaintenanceHandler(
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

    public async ValueTask<StartDirectMaintenanceResponse> Handle(
        StartDirectMaintenanceCommand command, CancellationToken cancellationToken)
    {
        var result = await MaintenanceStartFlow.ExecuteAsync(
            _context, _currentUser, _auditLog, _assetStatusChanger,
            new MaintenanceStartRequest(
                command.AssetId, MaintenanceRecordId: null, ExpectedVersion: 0,
                command.MaintenanceTypeId, command.PerformedBy, command.Description, command.IncidentId),
            cancellationToken);

        return new StartDirectMaintenanceResponse(
            result.MaintenanceRecordId, MaintenanceStatusCodes.EnCurso,
            result.AssetId, result.AssetStatusCode, result.StartedAt, result.Version);
    }
}
