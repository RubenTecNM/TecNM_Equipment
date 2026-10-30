using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Behaviors;
using TecnmInventario.Api.Core.Entities;
using TecnmInventario.Api.Features.Roles.AssignPermissions;

namespace TecnmInventario.Api.Tests.Integration;

/// <summary>
/// 2.9: dos asignaciones de permisos concurrentes con la MISMA ExpectedVersion.
/// Los permisos viven en role_permissions, así que sin tocar la fila del rol
/// las dos podrían confirmarse. El handler actualiza primero roles (token de
/// concurrencia sobre Version) y solo después role_permissions, dentro de la
/// misma transacción: exactamente una gana y la otra recibe 409.
/// </summary>
[Trait("Category", "Integration")]
public sealed class AssignPermissionsConcurrencyTests : PostgresTestBase
{
    private const int Iterations = 5;

    public AssignPermissionsConcurrencyTests(PostgresContainerFixture fixture)
        : base(fixture)
    {
    }

    [DockerFact]
    public async Task Dos_asignaciones_concurrentes_con_la_misma_version_solo_una_gana()
    {
        await using var db = await CreateDatabaseAsync();

        for (var iteration = 0; iteration < Iterations; iteration++)
        {
            var roleId = await SeedRoleAsync(db, $"AUD_C1_CONC_{iteration}");

            using var first = PipelineHarness.Create(db);
            using var second = PipelineHarness.Create(db);
            var barrier = new Barrier(2);

            var command = new AssignPermissionsCommand(
                roleId, 1, new[] { "assets.view", "assets.create" });

            var results = await Task.WhenAll(
                RunAsync(first, command, barrier),
                RunAsync(second, command, barrier));

            var winner = results.Single(result => result is null);
            Assert.Null(winner);
            var failure = Assert.Single(results, result => result is not null);
            Assert.IsType<DbUpdateConcurrencyException>(failure);

            // El ganador confirmó la versión 2 y los permisos nuevos.
            Assert.Equal(2, await db.ScalarAsync<int>(
                $"select version from inventario.roles where role_id = {roleId}"));
            Assert.Equal(2, await db.CountAsync(
                "role_permissions", $"role_id = {roleId}"));
            Assert.Equal(1, await db.CountAsync(
                "audit_log",
                $"table_name = 'roles' and record_id = {roleId} and action = 'UPDATE'"));
        }
    }

    private static async Task<Exception?> RunAsync(
        PipelineHarness pipeline,
        AssignPermissionsCommand command,
        Barrier barrier)
    {
        var audit = new AuditLogService(pipeline.Context, new FakeCurrentUser());
        var handler = new AssignPermissionsHandler(
            pipeline.Context,
            new RecordingRolePermissionService(),
            audit,
            pipeline.Hooks,
            new FakeCurrentUser());
        var behavior = new TransactionBehavior<AssignPermissionsCommand, AssignPermissionsResponse>(
            pipeline.Context, pipeline.Factory, pipeline.Hooks,
            NullLogger<TransactionBehavior<AssignPermissionsCommand, AssignPermissionsResponse>>.Instance);

        return await Task.Run(async () =>
        {
            // Se sincroniza lo máximo posible: ambos leen la versión 1 antes de
            // que ninguno confirme. Aun si el planificador los separa, el
            // resultado sigue siendo un 200 y un 409 (la regla es la misma).
            barrier.SignalAndWait();
            try
            {
                await behavior.Handle(
                    command,
                    (message, ct) => handler.Handle(message, ct),
                    CancellationToken.None);
                return (Exception?)null;
            }
            catch (Exception exception)
            {
                return exception;
            }
        });
    }

    private static async Task<long> SeedRoleAsync(PostgresDatabase db, string code)
    {
        await using var context = db.CreateContext();
        var now = DateTime.UtcNow;

        var view = await context.Permissions.SingleAsync(p => p.Code == "assets.view");
        var role = new Role
        {
            Code = code,
            Name = code,
            CreatedAt = now,
            UpdatedAt = now,
            Version = 1
        };
        role.Permissions.Add(view);

        context.Roles.Add(role);
        await context.SaveChangesAsync();
        return role.RoleId;
    }
}
