using System.Text.Json;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TecnmInventario.Api.Common.Behaviors;
using TecnmInventario.Api.Core.Entities;
using TecnmInventario.Api.Features.Auth.Register;
using TecnmInventario.Api.Features.Roles.AssignPermissions;
using TecnmInventario.Api.Features.Roles.CreateRole;

namespace TecnmInventario.Api.Tests;

/// <summary>
/// Subetapa B3: registro de usuarios, creación de roles y asignación de
/// permisos. Ningún snapshot incluye password_hash. La caché de permisos se
/// invalida solo en un hook post-commit.
/// </summary>
public class AuditB3Tests
{
    private static void SeedRole(TecnmInventario.Api.Data.TecnmInventarioDbContext context, string code = "ADMIN")
    {
        var now = DateTime.UtcNow;
        context.Roles.Add(new Role
        {
            RoleId = 1,
            Code = code,
            Name = code,
            CreatedAt = now,
            UpdatedAt = now,
            Version = 1
        });
        context.SaveChanges();
    }

    [Fact]
    public async Task Register_audita_INSERT_sin_password_hash_en_el_snapshot()
    {
        var context = TestDb.Create();
        using var _ = context;
        SeedRole(context);
        var audit = new RecordingAuditLogService();
        var handler = new RegisterHandler(context, new FakeCurrentUser(), audit);

        var response = await handler.Handle(
            new RegisterCommand(
                "aud_b3_user", "aud_b3_user@example.com", "ClaveSegura123!",
                "Nombre", "Apellido", "ADMIN"),
            CancellationToken.None);

        var entry = Assert.Single(audit.Entries);
        Assert.Equal("app_users", entry.TableName);
        Assert.Equal(response.UserId, entry.RecordId);
        Assert.Equal("INSERT", entry.Action);
        Assert.Null(entry.OldData);

        var json = JsonSerializer.Serialize(entry.NewData);
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hash", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ClaveSegura123!", json, StringComparison.Ordinal);
        Assert.Contains("aud_b3_user", json, StringComparison.Ordinal);
        Assert.Contains("\"Status\":\"activo\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateRole_audita_INSERT_con_oldData_nulo()
    {
        var context = TestDb.Create();
        using var _ = context;
        var audit = new RecordingAuditLogService();
        var handler = new CreateRoleHandler(context, new FakeCurrentUser(), audit);

        var response = await handler.Handle(
            new CreateRoleCommand("AUD_B3_ROLE", "Rol de auditoría B3", "rol de prueba"),
            CancellationToken.None);

        var entry = Assert.Single(audit.Entries);
        Assert.Equal("roles", entry.TableName);
        Assert.Equal(response.RoleId, entry.RecordId);
        Assert.Equal("INSERT", entry.Action);
        Assert.Null(entry.OldData);

        var json = JsonSerializer.Serialize(entry.NewData);
        Assert.Contains("AUD_B3_ROLE", json, StringComparison.Ordinal);
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
    }

    private static (TecnmInventario.Api.Data.TecnmInventarioDbContext Context, Role Role)
        CreateAssignHarness(params string[] assignedCodes)
        => CreateAssignHarness(null, assignedCodes);

    private static (TecnmInventario.Api.Data.TecnmInventarioDbContext Context, Role Role)
        CreateAssignHarness(
            SaveChangesCounter? counter,
            string[] assignedCodes,
            string[]? catalogCodes = null,
            int roleVersion = 1)
    {
        var context = counter is null ? TestDb.Create() : TestDb.Create(counter);
        var now = DateTime.UtcNow;
        catalogCodes ??= assignedCodes;

        var permissions = catalogCodes.Select((code, index) => new Permission
        {
            PermissionId = index + 1,
            Code = code,
            Name = code,
            CreatedAt = now,
            UpdatedAt = now,
            Version = 1
        }).ToList();

        var role = new Role
        {
            RoleId = 50,
            Code = "AUD_B3_ROLE",
            Name = "Rol de auditoría B3",
            CreatedAt = now,
            UpdatedAt = now,
            Version = roleVersion
        };

        foreach (var permission in permissions.Where(p => assignedCodes.Contains(p.Code)))
        {
            role.Permissions.Add(permission);
        }

        context.Permissions.AddRange(permissions);
        context.Roles.Add(role);
        context.SaveChanges();

        return (context, role);
    }

    private static AssignPermissionsHandler CreateAssignHandler(
        TecnmInventario.Api.Data.TecnmInventarioDbContext context,
        RecordingRolePermissionService rolePermissions,
        RecordingAuditLogService audit,
        ITransactionHooks hooks)
        => new(context, rolePermissions, audit, hooks, new FakeCurrentUser());

    [Fact]
    public async Task AssignPermissions_sin_cambios_no_guarda_no_audita_ni_invalida_cache()
    {
        var counter = new SaveChangesCounter();
        var (context, _) = CreateAssignHarness(counter, new[] { "assets.view" });
        using var __ = context;
        counter.Reset();

        var audit = new RecordingAuditLogService();
        var rolePermissions = new RecordingRolePermissionService();
        var hooks = new TransactionHooks(NullLogger<TransactionHooks>.Instance);
        var handler = CreateAssignHandler(context, rolePermissions, audit, hooks);

        var response = await handler.Handle(
            new AssignPermissionsCommand(50, 1, new[] { "assets.view" }),
            CancellationToken.None);

        Assert.Equal(0, counter.Count);
        Assert.Empty(audit.Entries);
        Assert.Empty(rolePermissions.InvalidatedCodes);
        Assert.Equal(new[] { "assets.view" }, response.Permissions);
    }

    [Fact]
    public async Task AssignPermissions_con_version_vieja_falla_aunque_no_haya_cambios()
    {
        // El rol ya está en versión 2: el cliente sigue viendo la 1 aunque el
        // conjunto de permisos enviado sea idéntico al actual. La regla es la
        // misma que UpdateAsset: versión vieja = 409 SIEMPRE.
        var counter = new SaveChangesCounter();
        var (context, _) = CreateAssignHarness(
            counter, new[] { "assets.view" }, roleVersion: 2);
        using var __ = context;
        counter.Reset();

        var audit = new RecordingAuditLogService();
        var rolePermissions = new RecordingRolePermissionService();
        var hooks = new TransactionHooks(NullLogger<TransactionHooks>.Instance);
        var handler = CreateAssignHandler(context, rolePermissions, audit, hooks);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => handler.Handle(
            new AssignPermissionsCommand(50, 1, new[] { "assets.view" }),
            CancellationToken.None).AsTask());

        Assert.Equal(0, counter.Count);
        Assert.Empty(audit.Entries);
        Assert.Empty(rolePermissions.InvalidatedCodes);
    }

    [Fact]
    public async Task AssignPermissions_con_version_vieja_y_cambios_falla_antes_de_escribir()
    {
        var counter = new SaveChangesCounter();
        var (context, _) = CreateAssignHarness(
            counter, new[] { "assets.view" }, new[] { "assets.view", "assets.create" },
            roleVersion: 2);
        using var __ = context;
        counter.Reset();

        var audit = new RecordingAuditLogService();
        var rolePermissions = new RecordingRolePermissionService();
        var hooks = new TransactionHooks(NullLogger<TransactionHooks>.Instance);
        var handler = CreateAssignHandler(context, rolePermissions, audit, hooks);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => handler.Handle(
            new AssignPermissionsCommand(50, 1, new[] { "assets.create" }),
            CancellationToken.None).AsTask());

        Assert.Equal(0, counter.Count);
        Assert.Empty(audit.Entries);
        Assert.Empty(rolePermissions.InvalidatedCodes);
    }

    [Fact]
    public async Task AssignPermissions_sin_cambios_devuelve_la_version_actual_del_rol()
    {
        var (context, _) = CreateAssignHarness(
            null, new[] { "assets.view" }, roleVersion: 3);
        using var __ = context;
        var audit = new RecordingAuditLogService();
        var rolePermissions = new RecordingRolePermissionService();
        var hooks = new TransactionHooks(NullLogger<TransactionHooks>.Instance);
        var handler = CreateAssignHandler(context, rolePermissions, audit, hooks);

        var response = await handler.Handle(
            new AssignPermissionsCommand(50, 3, new[] { "assets.view" }),
            CancellationToken.None);

        Assert.Equal(3, response.Version);
        Assert.Empty(audit.Entries);
    }

    [Fact]
    public async Task AssignPermissions_con_cambios_audita_UPDATE_y_los_permisos_van_ordenados()
    {
        var (context, _) = CreateAssignHarness(
            null, new[] { "assets.view" }, new[] { "assets.view", "assets.create" });
        using var __ = context;
        var audit = new RecordingAuditLogService();
        var rolePermissions = new RecordingRolePermissionService();
        var hooks = new TransactionHooks(NullLogger<TransactionHooks>.Instance);
        var handler = CreateAssignHandler(context, rolePermissions, audit, hooks);

        var response = await handler.Handle(
            new AssignPermissionsCommand(50, 1, new[] { "assets.view", "assets.create" }),
            CancellationToken.None);

        var entry = Assert.Single(audit.Entries);
        Assert.Equal("roles", entry.TableName);
        Assert.Equal(50, entry.RecordId);
        Assert.Equal("UPDATE", entry.Action);

        var oldJson = JsonSerializer.Serialize(entry.OldData);
        var newJson = JsonSerializer.Serialize(entry.NewData);
        Assert.Contains("assets.view", oldJson, StringComparison.Ordinal);
        Assert.Equal(
            new[] { "assets.create", "assets.view" },
            response.Permissions.ToArray());

        using var document = JsonDocument.Parse(newJson);
        var permissionsAfter = document.RootElement
            .GetProperty("Permissions")
            .EnumerateArray()
            .Select(e => e.GetString())
            .ToArray();
        Assert.Equal(new[] { "assets.create", "assets.view" }, permissionsAfter);
    }

    [Fact]
    public async Task AssignPermissions_bajo_el_behavior_invalida_la_cache_solo_despues_del_commit()
    {
        var (context, _) = CreateAssignHarness(
            null, new[] { "assets.view" }, new[] { "assets.view", "assets.create" });
        using var __ = context;
        var events = new List<string>();
        var factory = new FakeTransactionScopeFactory
        {
            OnCommit = () => events.Add("commit")
        };
        var rolePermissions = new RecordingRolePermissionService
        {
            OnInvalidate = _ => events.Add("invalidate")
        };
        var audit = new RecordingAuditLogService();
        var hooks = new TransactionHooks(NullLogger<TransactionHooks>.Instance);
        var handler = CreateAssignHandler(context, rolePermissions, audit, hooks);
        var behavior = new TransactionBehavior<AssignPermissionsCommand, AssignPermissionsResponse>(
            context, factory, hooks,
            NullLogger<TransactionBehavior<AssignPermissionsCommand, AssignPermissionsResponse>>.Instance);

        await behavior.Handle(
            new AssignPermissionsCommand(50, 1, new[] { "assets.view", "assets.create" }),
            (message, ct) => handler.Handle(message, ct),
            CancellationToken.None);

        Assert.Equal(1, factory.Scope!.CommitCount);
        Assert.Equal(new[] { "AUD_B3_ROLE" }, rolePermissions.InvalidatedCodes);
        Assert.Equal(new[] { "commit", "invalidate" }, events);
    }

    [Fact]
    public async Task AssignPermissions_si_falla_la_auditoria_no_invalida_la_cache()
    {
        var (context, _) = CreateAssignHarness(
            null, new[] { "assets.view" }, new[] { "assets.view", "assets.create" });
        using var __ = context;
        var factory = new FakeTransactionScopeFactory();
        var rolePermissions = new RecordingRolePermissionService();
        var audit = new RecordingAuditLogService { FailOnLog = true };
        var hooks = new TransactionHooks(NullLogger<TransactionHooks>.Instance);
        var handler = CreateAssignHandler(context, rolePermissions, audit, hooks);
        var behavior = new TransactionBehavior<AssignPermissionsCommand, AssignPermissionsResponse>(
            context, factory, hooks,
            NullLogger<TransactionBehavior<AssignPermissionsCommand, AssignPermissionsResponse>>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => behavior.Handle(
            new AssignPermissionsCommand(50, 1, new[] { "assets.view", "assets.create" }),
            (message, ct) => handler.Handle(message, ct),
            CancellationToken.None).AsTask());

        Assert.Empty(rolePermissions.InvalidatedCodes);
        Assert.Equal(1, factory.Scope!.RollbackCount);
        Assert.Equal(0, factory.Scope.CommitCount);
    }

    [Fact]
    public async Task AssignPermissions_si_falla_el_commit_no_invalida_la_cache()
    {
        var (context, _) = CreateAssignHarness(
            null, new[] { "assets.view" }, new[] { "assets.view", "assets.create" });
        using var __ = context;
        var factory = new FakeTransactionScopeFactory { CommitFails = true };
        var rolePermissions = new RecordingRolePermissionService();
        var audit = new RecordingAuditLogService();
        var hooks = new TransactionHooks(NullLogger<TransactionHooks>.Instance);
        var handler = CreateAssignHandler(context, rolePermissions, audit, hooks);
        var behavior = new TransactionBehavior<AssignPermissionsCommand, AssignPermissionsResponse>(
            context, factory, hooks,
            NullLogger<TransactionBehavior<AssignPermissionsCommand, AssignPermissionsResponse>>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => behavior.Handle(
            new AssignPermissionsCommand(50, 1, new[] { "assets.view", "assets.create" }),
            (message, ct) => handler.Handle(message, ct),
            CancellationToken.None).AsTask());

        Assert.Empty(rolePermissions.InvalidatedCodes);
        Assert.Equal(1, factory.Scope!.CommitCount);
        Assert.Equal(1, factory.Scope.RollbackCount);
    }
}
