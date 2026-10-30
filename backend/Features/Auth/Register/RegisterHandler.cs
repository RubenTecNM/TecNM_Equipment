using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Core.Entities;

namespace TecnmInventario.Api.Features.Auth.Register;

public sealed class RegisterHandler : ICommandHandler<RegisterCommand, RegisterResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;

    public RegisterHandler(
        TecnmInventarioDbContext context,
        ICurrentUserService currentUser,
        IAuditLogService auditLog)
    {
        _context = context;
        _currentUser = currentUser;
        _auditLog = auditLog;
    }

    public async ValueTask<RegisterResponse> Handle(RegisterCommand command, CancellationToken cancellationToken)
    {
        var role = await _context.Roles
            .FirstAsync(r => r.Code == command.RoleCode && !r.IsDeleted, cancellationToken);

        var now = DateTime.UtcNow;

        var user = new AppUser
        {
            Username = command.Username,
            Email = command.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(command.Password),
            FirstName = command.FirstName,
            LastName = command.LastName,
            RoleId = role.RoleId,
            Status = "activo",
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = _currentUser.UserId,
            IsDeleted = false,
            Version = 1
        };

        _context.AppUsers.Add(user);
        await _context.SaveChangesAsync(cancellationToken);

        // Proyección explícita: NUNCA se incluye PasswordHash en audit_log.
        await _auditLog.LogAsync(
            tableName: "app_users",
            recordId: user.UserId,
            action: "INSERT",
            oldData: null,
            newData: new
            {
                user.UserId,
                user.Username,
                user.Email,
                user.FirstName,
                user.LastName,
                user.RoleId,
                user.Status,
                user.Version
            },
            cancellationToken);

        return new RegisterResponse(user.UserId, user.Username, user.Email, role.Code);
    }
}
