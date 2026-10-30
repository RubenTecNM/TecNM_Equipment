using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Locations.UpdateLocation;

public sealed class UpdateLocationHandler : ICommandHandler<UpdateLocationCommand, UpdateLocationResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;

    public UpdateLocationHandler(
        TecnmInventarioDbContext context,
        ICurrentUserService currentUser,
        IAuditLogService auditLog)
    {
        _context = context;
        _currentUser = currentUser;
        _auditLog = auditLog;
    }

    public async ValueTask<UpdateLocationResponse> Handle(
        UpdateLocationCommand command, CancellationToken cancellationToken)
    {
        var location = await _context.Locations
            .FirstOrDefaultAsync(l => l.LocationId == command.LocationId, cancellationToken);

        if (location is null)
        {
            throw new KeyNotFoundException("La ubicación solicitada no existe.");
        }

        // Concurrencia optimista antes del atajo de "sin cambios": una versión
        // vieja siempre es 409, aunque los datos enviados sean idénticos.
        if (location.Version != command.ExpectedVersion)
        {
            throw new DbUpdateConcurrencyException(
                "La ubicación fue modificada por otro usuario mientras tanto.");
        }

        var name = command.Name.Trim();

        // Un PUT idéntico no genera UPDATE, ni sube Version, ni se audita.
        var current = (location.Name, location.LocationTypeId, location.ParentLocationId);
        var requested = (name, command.LocationTypeId, command.ParentLocationId);
        if (current == requested)
        {
            return new UpdateLocationResponse(location.LocationId, location.Version);
        }

        // Snapshot "antes" antes de mutar.
        var before = new
        {
            location.Code,
            location.Name,
            location.LocationTypeId,
            location.ParentLocationId,
            location.Version
        };

        _context.Entry(location).Property(l => l.Version).OriginalValue = command.ExpectedVersion;

        location.Name = name;
        location.LocationTypeId = command.LocationTypeId;
        location.ParentLocationId = command.ParentLocationId;
        location.UpdatedBy = _currentUser.UserId;

        await _context.SaveChangesAsync(cancellationToken);
        await _context.Entry(location).ReloadAsync(cancellationToken);

        var after = new
        {
            location.Code,
            location.Name,
            location.LocationTypeId,
            location.ParentLocationId,
            location.Version
        };

        await _auditLog.LogAsync(
            tableName: "locations",
            recordId: location.LocationId,
            action: "UPDATE",
            oldData: before,
            newData: after,
            cancellationToken);

        return new UpdateLocationResponse(location.LocationId, location.Version);
    }
}
