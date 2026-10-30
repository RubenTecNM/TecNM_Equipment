using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Authorization;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Incidents.AssignIncident;

public sealed class AssignIncidentHandler : ICommandHandler<AssignIncidentCommand, AssignIncidentResponse>
{
    private const string ResolvePermission = "incidents.resolve";

    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;
    private readonly IRolePermissionService _rolePermissions;

    public AssignIncidentHandler(
        TecnmInventarioDbContext context,
        ICurrentUserService currentUser,
        IAuditLogService auditLog,
        IRolePermissionService rolePermissions)
    {
        _context = context;
        _currentUser = currentUser;
        _auditLog = auditLog;
        _rolePermissions = rolePermissions;
    }

    public async ValueTask<AssignIncidentResponse> Handle(
        AssignIncidentCommand command, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();

        var incident = await _context.Incidents
            .Include(i => i.IncidentStatus)
            .FirstOrDefaultAsync(i => i.IncidentId == command.IncidentId, cancellationToken)
            ?? throw new KeyNotFoundException("El incidente solicitado no existe.");

        var statusCode = incident.IncidentStatus.Code;
        if (statusCode is IncidentStatusCodes.Resuelto or IncidentStatusCodes.Cerrado)
        {
            throw new BusinessRuleViolationException(
                "No se puede reasignar un incidente resuelto o cerrado.");
        }

        if (incident.AssignedTo == command.AssignedToUserId)
        {
            throw new BusinessRuleViolationException(
                "El incidente ya está asignado a ese técnico.");
        }

        // Mensaje único para "no existe" e "inactivo": no revela qué usuarios existen.
        var technician = await _context.AppUsers
            .Where(u => u.UserId == command.AssignedToUserId && !u.IsDeleted && u.Status == "activo")
            .Select(u => new { u.UserId, u.Username, u.RoleId })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new BusinessRuleViolationException(
                "El técnico especificado no existe o no está activo.");

        var roleCode = await _context.Roles
            .Where(r => r.RoleId == technician.RoleId)
            .Select(r => r.Code)
            .FirstAsync(cancellationToken);

        if (!await _rolePermissions.HasPermissionAsync(roleCode, ResolvePermission, cancellationToken))
        {
            throw new BusinessRuleViolationException(
                "El usuario especificado no tiene permisos para atender incidentes.");
        }

        var previousAssignee = incident.AssignedTo;

        _context.Entry(incident).Property(i => i.Version).OriginalValue = command.ExpectedVersion;
        incident.AssignedTo = technician.UserId;
        incident.UpdatedBy = userId;

        // Transacción explícita: el cambio y su fila de audit_log son atómicos.
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
        await _context.Entry(incident).ReloadAsync(cancellationToken);

        await _auditLog.LogAsync(
            tableName: "incidents",
            recordId: incident.IncidentId,
            action: "UPDATE",
            oldData: new { AssignedTo = previousAssignee },
            newData: new { AssignedTo = technician.UserId, AssignedToUsername = technician.Username },
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new AssignIncidentResponse(
            incident.IncidentId, technician.UserId, technician.Username, incident.Version);
    }
}
