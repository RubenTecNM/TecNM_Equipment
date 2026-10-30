using TecnmInventario.Api.Common.Authorization;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Security;

namespace TecnmInventario.Api.Features.Documents;

public sealed class DocumentAccessAuthorizer : IDocumentAccessAuthorizer
{
    private readonly IRolePermissionService _rolePermissionService;
    private readonly ICurrentUserService _currentUser;

    public DocumentAccessAuthorizer(
        IRolePermissionService rolePermissionService,
        ICurrentUserService currentUser)
    {
        _rolePermissionService = rolePermissionService;
        _currentUser = currentUser;
    }

    public async Task EnsureAuthorizedAsync(string ownerType, CancellationToken cancellationToken)
    {
        var roleCode = _currentUser.RoleCode;
        if (string.IsNullOrEmpty(roleCode))
            throw new ForbiddenAccessException("No se pudo determinar el rol del usuario actual.");

        var requiredPermissions = DocumentAuthorizationMap.GetRequiredPermissions(ownerType);

        foreach (var permission in requiredPermissions)
        {
            if (await _rolePermissionService.HasPermissionAsync(roleCode, permission, cancellationToken))
                return;
        }

        throw new ForbiddenAccessException(
            $"El rol {roleCode} no tiene permiso para acceder a documentos de tipo {ownerType}.");
    }
}
