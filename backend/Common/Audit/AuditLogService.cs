using System.Text.Json;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Core.Entities;

namespace TecnmInventario.Api.Common.Audit;

public sealed class AuditLogService : IAuditLogService
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public AuditLogService(TecnmInventarioDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task LogAsync(
        string tableName,
        long recordId,
        string action,
        object? oldData,
        object? newData,
        CancellationToken cancellationToken)
    {
        var entry = new AuditLog
        {
            TableName = tableName,
            RecordId = recordId,
            Action = action,
            OldData = oldData is null ? null : JsonSerializer.Serialize(oldData),
            NewData = newData is null ? null : JsonSerializer.Serialize(newData),
            PerformedBy = _currentUser.UserId,
            PerformedAt = DateTime.UtcNow,
            ClientIp = _currentUser.IpAddress
        };

        _context.AuditLogs.Add(entry);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
