using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Authorization;
using TecnmInventario.Api.Common.Behaviors;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Roles.AssignPermissions;

public sealed class AssignPermissionsHandler : ICommandHandler<AssignPermissionsCommand, AssignPermissionsResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly IRolePermissionService _rolePermissionService;
    private readonly IAuditLogService _auditLog;
    private readonly ITransactionHooks _hooks;
    private readonly ICurrentUserService _currentUser;

    public AssignPermissionsHandler(
        TecnmInventarioDbContext context,
        IRolePermissionService rolePermissionService,
        IAuditLogService auditLog,
        ITransactionHooks hooks,
        ICurrentUserService currentUser)
    {
        _context = context;
        _rolePermissionService = rolePermissionService;
        _auditLog = auditLog;
        _hooks = hooks;
        _currentUser = currentUser;
    }

    public async ValueTask<AssignPermissionsResponse> Handle(
        AssignPermissionsCommand command, CancellationToken cancellationToken)
    {
        var role = await _context.Roles
            .Include(r => r.Permissions)
            .FirstAsync(r => r.RoleId == command.RoleId && !r.IsDeleted, cancellationToken);

        // Concurrencia optimista (como UpdateAsset): la versión que vio el
        // cliente debe coincidir SIEMPRE, incluso si el conjunto de permisos
        // enviado es idéntico al actual.
        if (role.Version != command.ExpectedVersion)
        {
            throw new DbUpdateConcurrencyException(
                "El rol fue modificado por otro usuario mientras tanto.");
        }

        var desiredCodes = command.PermissionCodes.ToHashSet();
        var currentCodes = role.Permissions.Select(p => p.Code).ToHashSet();

        var codesToRemove = currentCodes.Except(desiredCodes).ToList();
        var codesToAdd = desiredCodes.Except(currentCodes).ToList();

        // Sin altas ni bajas reales no hay escritura: ni SaveChanges, ni
        // audit_log, ni invalidación de caché. Con ExpectedVersion vieja se
        // lanza 409 antes de llegar aquí.
        if (codesToRemove.Count == 0 && codesToAdd.Count == 0)
        {
            var unchangedCodes = role.Permissions.Select(p => p.Code).OrderBy(c => c).ToList();
            return new AssignPermissionsResponse(role.RoleId, role.Code, unchangedCodes, role.Version);
        }

        // Snapshot "antes", ordenado para que la comparación sea estable.
        var beforeCodes = role.Permissions.Select(p => p.Code).OrderBy(c => c).ToList();
        var beforeVersion = role.Version;

        // La caché se invalida SOLO después del commit: si el SaveChanges o
        // audit_log fallan, la transacción se revierte y el hook de commit se
        // descarta, de modo que la caché sigue reflejando el estado real.
        _hooks.OnCommit(() =>
        {
            _rolePermissionService.InvalidateCache(role.Code);
            return Task.CompletedTask;
        });

        // 1) Tocar la fila del rol PRIMERO. Los permisos viven en
        // role_permissions, así que sin este UPDATE la versión del rol no
        // cambiaría y dos asignaciones concurrentes podrían confirmarse las
        // dos. El token de concurrencia añade "WHERE version = ExpectedVersion":
        // si otra asignación ganó la carrera, afecta 0 filas y EF lanza
        // DbUpdateConcurrencyException (-> 409). Además, al bloquear la fila
        // del rol antes de tocar role_permissions se impone un orden de
        // bloqueo estable y se evitan deadlocks entre asignaciones.
        role.UpdatedAt = DateTime.UtcNow;
        role.UpdatedBy = _currentUser.UserId;
        _context.Entry(role).Property(r => r.Version).OriginalValue = command.ExpectedVersion;

        await _context.SaveChangesAsync(cancellationToken);

        // El trigger touch_version subió version en la BD: releer para que la
        // respuesta y el snapshot lleven la versión real.
        await _context.Entry(role).ReloadAsync(cancellationToken);

        // 2) Altas y bajas reales en role_permissions, en la misma transacción.
        if (codesToRemove.Count > 0)
        {
            var permissionsToRemove = role.Permissions
                .Where(p => codesToRemove.Contains(p.Code))
                .ToList();

            foreach (var permission in permissionsToRemove)
            {
                role.Permissions.Remove(permission);
            }
        }

        if (codesToAdd.Count > 0)
        {
            var permissionsToAdd = await _context.Permissions
                .Where(p => codesToAdd.Contains(p.Code) && !p.IsDeleted)
                .ToListAsync(cancellationToken);

            foreach (var permission in permissionsToAdd)
            {
                role.Permissions.Add(permission);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        var finalCodes = role.Permissions.Select(p => p.Code).OrderBy(c => c).ToList();

        await _auditLog.LogAsync(
            tableName: "roles",
            recordId: role.RoleId,
            action: "UPDATE",
            oldData: new { Permissions = beforeCodes, Version = beforeVersion },
            newData: new { Permissions = finalCodes, Version = role.Version },
            cancellationToken);

        return new AssignPermissionsResponse(role.RoleId, role.Code, finalCodes, role.Version);
    }
}
