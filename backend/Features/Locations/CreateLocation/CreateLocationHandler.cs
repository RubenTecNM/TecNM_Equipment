using Mediator;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Core.Entities;

namespace TecnmInventario.Api.Features.Locations.CreateLocation;

public sealed class CreateLocationHandler : ICommandHandler<CreateLocationCommand, CreateLocationResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;

    public CreateLocationHandler(
        TecnmInventarioDbContext context,
        ICurrentUserService currentUser,
        IAuditLogService auditLog)
    {
        _context = context;
        _currentUser = currentUser;
        _auditLog = auditLog;
    }

    public async ValueTask<CreateLocationResponse> Handle(
        CreateLocationCommand command, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var location = new Location
        {
            Code = command.Code.Trim(),
            Name = command.Name.Trim(),
            LocationTypeId = command.LocationTypeId,
            ParentLocationId = command.ParentLocationId,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = _currentUser.UserId,
            IsDeleted = false,
            Version = 1
        };

        _context.Locations.Add(location);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditLog.LogAsync(
            tableName: "locations",
            recordId: location.LocationId,
            action: "INSERT",
            oldData: null,
            newData: new
            {
                location.Code,
                location.Name,
                location.LocationTypeId,
                location.ParentLocationId,
                location.Version
            },
            cancellationToken);

        return new CreateLocationResponse(location.LocationId, location.Code, location.Version);
    }
}
