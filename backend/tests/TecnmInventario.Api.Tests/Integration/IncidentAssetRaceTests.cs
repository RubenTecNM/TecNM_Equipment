using Npgsql;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Assets.Shared;
using TecnmInventario.Api.Features.Incidents.CreateIncident;
using TecnmInventario.Api.Features.Incidents.ResolveIncident;
using TecnmInventario.Api.Features.Incidents.UpdateIncidentStatus;

namespace TecnmInventario.Api.Tests.Integration;

/// <summary>
/// Reproduce la carrera de resolución de incidentes (Lote 3, C2) contra
/// PostgreSQL real. No toca código de producción: solo ejercita los handlers
/// con un DbContext propio por tarea y sincroniza el arranque con un Barrier.
/// </summary>
[Trait("Category", "Integration")]
public sealed class IncidentAssetRaceTests : PostgresTestBase
{
    private const int Iterations = 20;
    private static readonly string[] OpenCodes =
    {
        IncidentStatusCodes.Abierto,
        IncidentStatusCodes.EnDiagnostico,
        IncidentStatusCodes.EnReparacion
    };

    public IncidentAssetRaceTests(PostgresContainerFixture fixture)
        : base(fixture)
    {
    }

    [DockerFact]
    public async Task Resolver_dos_incidentes_del_mismo_activo_en_paralelo_no_lo_deja_en_CON_FALLA_sin_abiertos()
    {
        await using var db = await CreateDatabaseAsync();
        var failures = new List<string>();
        var deadlocks = 0;

        for (var iteration = 1; iteration <= Iterations; iteration++)
        {
            var (assetId, incidentA, incidentB) = await SeedAssetWithOpenIncidentsAsync(db, iteration);
            var barrier = new Barrier(2);

            var first = Task.Run(() => ResolveAsync(db, incidentA, barrier));
            var second = Task.Run(() => ResolveAsync(db, incidentB, barrier));
            var exceptions = (await Task.WhenAll(first, second))
                .Where(e => e is not null)
                .ToArray();

            var state = await ReadStateAsync(db, assetId);
            deadlocks += exceptions.Count(exception => IsDeadlock(exception!));

            foreach (var exception in exceptions)
            {
                failures.Add(
                    $"iteración {iteration}: excepción {exception!.GetType().Name}: {exception.Message}\n{exception.StackTrace}");
            }

            if (state.OpenCount != 0 || state.AssetCode != AssetStatusCodes.Disponible)
            {
                failures.Add(
                    $"iteración {iteration}: activo {assetId} quedó {state.AssetCode} con {state.OpenCount} incidentes abiertos; se esperaba DISPONIBLE con 0");
            }
        }

        Assert.True(
            failures.Count == 0 && deadlocks == 0,
            $"{failures.Count}/{Iterations} iteraciones rompieron el invariante " +
            $"(se esperaba DISPONIBLE con 0 abiertos) o lanzaron excepción; " +
            $"deadlocks 40P01: {deadlocks}:\n" +
            string.Join("\n", failures));
    }

    [DockerFact]
    public async Task Crear_un_incidente_mientras_se_resuelve_otro_nunca_deja_el_activo_liberado_con_uno_abierto()
    {
        await using var db = await CreateDatabaseAsync();
        var incidentTypeId = await ScalarAsync<long>(
            db, "select incident_type_id from inventario.incident_types where code = 'HARDWARE'");
        var incidentPriorityId = await ScalarAsync<long>(
            db, "select incident_priority_id from inventario.incident_priorities where code = 'ALTA'");
        var failures = new List<string>();
        var deadlocks = 0;

        for (var iteration = 1; iteration <= Iterations; iteration++)
        {
            var (assetId, incidentA, _) = await SeedAssetWithOpenIncidentsAsync(db, iteration, incidentCount: 1);
            var barrier = new Barrier(2);

            var resolve = Task.Run(() => ResolveAsync(db, incidentA, barrier));
            var create = Task.Run(() => CreateAsync(
                db, assetId, incidentTypeId, incidentPriorityId, barrier));
            var exceptions = (await Task.WhenAll(resolve, create))
                .Where(e => e is not null)
                .ToArray();

            var state = await ReadStateAsync(db, assetId);
            deadlocks += exceptions.Count(exception => IsDeadlock(exception!));

            foreach (var exception in exceptions)
            {
                failures.Add(
                    $"iteración {iteration}: excepción {exception!.GetType().Name}: {exception.Message}\n{exception.StackTrace}");
            }

            var hasOpen = state.OpenCount > 0;
            var isConFalla = state.AssetCode == AssetStatusCodes.ConFalla;
            if (hasOpen != isConFalla || state.OpenCount != 1)
            {
                failures.Add(
                    $"iteración {iteration}: activo {assetId} quedó {state.AssetCode} con {state.OpenCount} incidente(s) abierto(s); " +
                    "se esperaba CON_FALLA con exactamente 1 abierto (abiertos>0 <=> CON_FALLA)");
            }
        }

        Assert.True(
            failures.Count == 0 && deadlocks == 0,
            $"{failures.Count}/{Iterations} iteraciones rompieron el invariante " +
            $"(abiertos>0 <=> CON_FALLA) o lanzaron excepción; " +
            $"deadlocks 40P01: {deadlocks}:\n" +
            string.Join("\n", failures));
    }

    // ---------------- lock determinista (f) ----------------

    [DockerFact]
    public async Task ResolveIncident_espera_el_lock_de_la_fila_del_activo()
    {
        await using var db = await CreateDatabaseAsync();
        var (assetId, incidentA, _) = await SeedAssetWithOpenIncidentsAsync(db, 1, incidentCount: 1);

        await AssertHandlerWaitsForAssetLockAsync(
            db, assetId, () => ResolveAsync(db, incidentA, barrier: null), "ResolveIncident");
    }

    [DockerFact]
    public async Task CreateIncident_espera_el_lock_de_la_fila_del_activo()
    {
        await using var db = await CreateDatabaseAsync();
        var (assetId, _, _) = await SeedAssetWithOpenIncidentsAsync(
            db, 1, incidentCount: 0, assetStatusCode: AssetStatusCodes.Disponible);
        var incidentTypeId = await ScalarAsync<long>(
            db, "select incident_type_id from inventario.incident_types where code = 'HARDWARE'");
        var incidentPriorityId = await ScalarAsync<long>(
            db, "select incident_priority_id from inventario.incident_priorities where code = 'ALTA'");

        await AssertHandlerWaitsForAssetLockAsync(
            db, assetId,
            () => CreateAsync(db, assetId, incidentTypeId, incidentPriorityId, barrier: null),
            "CreateIncident");
    }

    [DockerFact]
    public async Task UpdateIncidentStatus_espera_el_lock_de_la_fila_del_activo()
    {
        await using var db = await CreateDatabaseAsync();
        var (assetId, incidentA, _) = await SeedAssetWithOpenIncidentsAsync(db, 1, incidentCount: 1);

        await AssertHandlerWaitsForAssetLockAsync(
            db, assetId, () => UpdateStatusAsync(db, incidentA, barrier: null), "UpdateIncidentStatus");
    }

    // ---------------- helpers ----------------

    private static async Task AssertHandlerWaitsForAssetLockAsync(
        PostgresDatabase db,
        long assetId,
        Func<Task<Exception?>> runHandler,
        string handlerName)
    {
        await using var connection = await db.OpenConnectionAsync();
        await using var transaction = connection.BeginTransaction();
        await using (var command = new NpgsqlCommand(
            "select asset_id from inventario.assets where asset_id = @asset_id for update",
            connection,
            transaction))
        {
            command.Parameters.AddWithValue("asset_id", assetId);
            Assert.NotNull(await command.ExecuteScalarAsync());
        }

        var handlerTask = Task.Run(runHandler);

        await Task.Delay(TimeSpan.FromMilliseconds(500));
        Assert.False(
            handlerTask.IsCompleted,
            $"{handlerName} terminó con el activo bloqueado por otra transacción.");

        // Además de no terminar, debe estar esperando un lock de la base.
        // PostgreSQL representa la espera por una fila como un lock de
        // transacción no concedido (o de tuple según la versión). Se sondea
        // unos segundos para no depender de que el handler ya haya llegado.
        long waitingLocks = 0;
        for (var attempt = 0; attempt < 50 && waitingLocks == 0 && !handlerTask.IsCompleted; attempt++)
        {
            waitingLocks = await ScalarAsync<long>(db, """
                select count(*)
                from pg_stat_activity
                where datname = current_database()
                  and wait_event_type = 'Lock'
                  and pid <> pg_backend_pid()
                """);

            if (waitingLocks == 0)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100));
            }
        }

        Assert.False(
            handlerTask.IsCompleted,
            $"{handlerName} terminó sin esperar el lock del activo.");
        Assert.True(
            waitingLocks > 0,
            $"{handlerName} no está esperando un lock (pg_locks no muestra waiters).");

        await transaction.CommitAsync();

        var completed = await Task.WhenAny(handlerTask, Task.Delay(TimeSpan.FromSeconds(15)));
        Assert.Same(handlerTask, completed);
        var exception = await handlerTask;
        Assert.True(
            exception is null,
            $"{handlerName} falló después de liberar el lock: {exception}");
    }

    private static bool IsDeadlock(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException { SqlState: "40P01" })
            {
                return true;
            }
        }

        return false;
    }

    private static async Task<Exception?> ResolveAsync(
        PostgresDatabase db, long incidentId, Barrier? barrier)
    {
        await using var context = new TecnmInventarioDbContext(db.CreateOptions());
        var currentUser = new FakeCurrentUser();
        var audit = new AuditLogService(context, currentUser);
        var handler = new ResolveIncidentHandler(
            context, currentUser, audit, new AssetStatusChanger(context, currentUser, audit));

        barrier?.SignalAndWait(TimeSpan.FromSeconds(30));

        try
        {
            await handler.Handle(
                new ResolveIncidentCommand(incidentId, 1, "Solución de prueba", null),
                CancellationToken.None);
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private static async Task<Exception?> CreateAsync(
        PostgresDatabase db,
        long assetId,
        long incidentTypeId,
        long incidentPriorityId,
        Barrier? barrier)
    {
        await using var context = new TecnmInventarioDbContext(db.CreateOptions());
        var currentUser = new FakeCurrentUser();
        var audit = new AuditLogService(context, currentUser);
        var handler = new CreateIncidentHandler(
            context, currentUser, audit, new AssetStatusChanger(context, currentUser, audit));

        barrier?.SignalAndWait(TimeSpan.FromSeconds(30));

        try
        {
            await handler.Handle(
                new CreateIncidentCommand(
                    assetId, incidentTypeId, incidentPriorityId, "Falla concurrente", null),
                CancellationToken.None);
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private static async Task<Exception?> UpdateStatusAsync(
        PostgresDatabase db, long incidentId, Barrier? barrier)
    {
        await using var context = new TecnmInventarioDbContext(db.CreateOptions());
        var currentUser = new FakeCurrentUser();
        var audit = new AuditLogService(context, currentUser);
        var handler = new UpdateIncidentStatusHandler(
            context, currentUser, audit, new AssetStatusChanger(context, currentUser, audit));

        barrier?.SignalAndWait(TimeSpan.FromSeconds(30));

        try
        {
            await handler.Handle(
                new UpdateIncidentStatusCommand(
                    incidentId, 1, IncidentStatusCodes.EnReparacion, "Reparación de prueba"),
                CancellationToken.None);
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private static async Task<(long AssetId, long IncidentA, long IncidentB)> SeedAssetWithOpenIncidentsAsync(
        PostgresDatabase db,
        int iteration,
        int incidentCount = 2,
        string assetStatusCode = AssetStatusCodes.ConFalla)
    {
        var inventoryCode = $"RACE-{iteration}-{Guid.NewGuid():N}"[..24];
        var assetId = await ScalarAsync<long>(db, $"""
            insert into inventario.assets (inventory_code, asset_type_id, asset_status_id, created_by)
            select '{inventoryCode}', at.asset_type_id, st.asset_status_id, 1
            from inventario.asset_types at, inventario.asset_statuses st
            where at.code = 'PC' and st.code = '{assetStatusCode}'
            returning asset_id
            """);

        var incidentA = incidentCount > 0
            ? await AddIncidentAsync(db, assetId, "Falla A")
            : 0;
        var incidentB = incidentCount > 1
            ? await AddIncidentAsync(db, assetId, "Falla B")
            : 0;

        return (assetId, incidentA, incidentB);
    }

    private static Task<long> AddIncidentAsync(PostgresDatabase db, long assetId, string title)
        => ScalarAsync<long>(db, $"""
            insert into inventario.incidents
                (asset_id, incident_type_id, incident_priority_id, incident_status_id,
                 reported_by, title, created_by)
            select {assetId}, it.incident_type_id, ip.incident_priority_id,
                   ist.incident_status_id, 1, '{title}', 1
            from inventario.incident_types it, inventario.incident_priorities ip,
                 inventario.incident_statuses ist
            where it.code = 'HARDWARE' and ip.code = 'ALTA'
              and ist.code = '{IncidentStatusCodes.Abierto}'
            returning incident_id
            """);

    private static async Task<(string AssetCode, int OpenCount)> ReadStateAsync(
        PostgresDatabase db, long assetId)
    {
        var codes = string.Join(", ", OpenCodes.Select(code => $"'{code}'"));
        var raw = await ScalarAsync<string>(db, $"""
            select st.code || '|' || (
                select count(*)
                from inventario.incidents i
                join inventario.incident_statuses s
                  on s.incident_status_id = i.incident_status_id
                where i.asset_id = {assetId} and not i.is_deleted
                  and s.code in ({codes}))
            from inventario.assets a
            join inventario.asset_statuses st on st.asset_status_id = a.asset_status_id
            where a.asset_id = {assetId}
            """);

        var parts = raw.Split('|');
        return (parts[0], int.Parse(parts[1]));
    }

    private static async Task<T> ScalarAsync<T>(PostgresDatabase db, string sql)
    {
        await using var connection = await db.OpenConnectionAsync();
        await using var command = new Npgsql.NpgsqlCommand(sql, connection);
        var value = await command.ExecuteScalarAsync();
        return (T)Convert.ChangeType(value!, typeof(T));
    }
}
