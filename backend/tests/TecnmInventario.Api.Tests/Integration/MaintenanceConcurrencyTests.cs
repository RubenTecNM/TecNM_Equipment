using System.Net;
using Npgsql;

namespace TecnmInventario.Api.Tests.Integration;

// ═══════════════════════════════════════════════════════════════════════════
// M.2: concurrencia y orden de locks (regla 19: el activo es SIEMPRE el primer
// lock). Contra PostgreSQL real, por HTTP. Datos ficticios.
// ═══════════════════════════════════════════════════════════════════════════

[Trait("Category", "Integration")]
public sealed class MaintenanceConcurrencyTests : MaintenanceTestBase
{
    private const int Iterations = 12;

    public MaintenanceConcurrencyTests(PostgresContainerFixture fixture)
        : base(fixture) { }

    // Dos Start directos simultáneos sobre el mismo activo: exactamente uno gana;
    // el otro recibe 400 (regla de negocio) o 409, nunca un 500.
    [DockerFact]
    public async Task Start_DosSimultaneosEnElMismoActivo_ExactamenteUnoGana_NuncaUn500()
    {
        await using var env = await CreateEnvAsync();
        var failures = new List<string>();

        for (var iteration = 1; iteration <= Iterations; iteration++)
        {
            var assetId = await env.InsertAssetAsync();
            var gate = new TaskCompletionSource();

            Task<HttpStatusCode> Fire() => Task.Run(async () =>
            {
                await gate.Task;
                return (await env.StartDirectAsync(assetId)).StatusCode;
            });

            var first = Fire();
            var second = Fire();
            gate.SetResult();
            var codes = await Task.WhenAll(first, second);

            var winners = codes.Count(c => c == HttpStatusCode.Created);
            var losers = codes.Count(c => c is HttpStatusCode.BadRequest or HttpStatusCode.Conflict);
            var inProgress = await env.Db.CountAsync("maintenance_records",
                $"asset_id = {assetId} AND status = 'EN_CURSO' AND NOT is_deleted");
            var status = await env.AssetStatusAsync(assetId);

            if (winners != 1 || losers != 1 || inProgress != 1 || status != "EN_MANTENIMIENTO")
            {
                failures.Add($"iteración {iteration}: códigos [{string.Join(", ", codes)}], " +
                             $"EN_CURSO={inProgress}, activo={status}");
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    // Start de un registro PROGRAMADO concurrente con un Start directo del mismo activo.
    [DockerFact]
    public async Task Start_ProgramadoYDirectoSimultaneos_ExactamenteUnoGana()
    {
        await using var env = await CreateEnvAsync();
        var failures = new List<string>();

        for (var iteration = 1; iteration <= Iterations; iteration++)
        {
            var assetId = await env.InsertAssetAsync();
            var scheduledId = await env.InsertScheduledAsync(assetId);
            var gate = new TaskCompletionSource();

            var fromScheduled = Task.Run(async () =>
            {
                await gate.Task;
                return (await env.StartAsync(scheduledId, 1)).StatusCode;
            });
            var direct = Task.Run(async () =>
            {
                await gate.Task;
                return (await env.StartDirectAsync(assetId)).StatusCode;
            });
            gate.SetResult();
            var codes = await Task.WhenAll(fromScheduled, direct);

            var winners = codes.Count(c => c is HttpStatusCode.OK or HttpStatusCode.Created);
            var inProgress = await env.Db.CountAsync("maintenance_records",
                $"asset_id = {assetId} AND status = 'EN_CURSO' AND NOT is_deleted");

            if (winners != 1 || inProgress != 1 || codes.Any(c => (int)c >= 500))
                failures.Add($"iteración {iteration}: códigos [{string.Join(", ", codes)}], EN_CURSO={inProgress}");
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    // Start vs Cancel del MISMO registro PROGRAMADO: uno gana, el otro 400 o 409, nunca 500.
    [DockerFact]
    public async Task StartYCancel_DelMismoRegistro_SimultaneosNuncaDan500()
    {
        await using var env = await CreateEnvAsync();
        var failures = new List<string>();

        for (var iteration = 1; iteration <= Iterations; iteration++)
        {
            var assetId = await env.InsertAssetAsync();
            var id = await env.InsertScheduledAsync(assetId);
            var gate = new TaskCompletionSource();

            var start = Task.Run(async () =>
            {
                await gate.Task;
                return (await env.StartAsync(id, 1)).StatusCode;
            });
            var cancel = Task.Run(async () =>
            {
                await gate.Task;
                return (await env.CancelAsync(id, 1)).StatusCode;
            });
            gate.SetResult();
            var codes = await Task.WhenAll(start, cancel);

            var status = await env.RecordStatusAsync(id);
            var assetStatus = await env.AssetStatusAsync(assetId);
            var consistent = (status == "EN_CURSO" && assetStatus == "EN_MANTENIMIENTO")
                          || (status == "CANCELADO" && assetStatus == "DISPONIBLE");

            if (codes.Any(c => (int)c >= 500) || codes.Count(c => c == HttpStatusCode.OK) != 1 || !consistent)
                failures.Add($"iteración {iteration}: códigos [{string.Join(", ", codes)}], registro={status}, activo={assetStatus}");
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    // ── Orden de locks: el activo SIEMPRE primero ──────────────────────────
    //
    // Se retiene el lock de la fila del activo desde otra conexión, se lanza la
    // operación y se espera a que quede bloqueada. Si el handler ya hubiera
    // bloqueado el REGISTRO antes que el activo, otra conexión no podría tomar
    // el registro con NOWAIT (55P03): con el orden correcto el registro sigue libre.
    [DockerTheory]
    [InlineData("start")]
    [InlineData("complete")]
    [InlineData("cancel")]
    public async Task OrdenDeLocks_ElActivoSeBloqueaAntesQueElRegistro(string operation)
    {
        await using var env = await CreateEnvAsync();
        var assetId = await env.InsertAssetAsync();
        long recordId;
        int version;
        if (operation == "start")
        {
            recordId = await env.InsertScheduledAsync(assetId);
            version = 1;
        }
        else
        {
            var started = await env.StartDirectAsync(assetId);
            Assert.Equal(HttpStatusCode.Created, started.StatusCode);
            var body = await MaintenanceEnv.ReadAsync(started);
            recordId = body.GetProperty("maintenanceRecordId").GetInt64();
            version = body.GetProperty("version").GetInt32();
        }

        await using var holder = await env.Db.OpenConnectionAsync();
        await using var holderTx = await holder.BeginTransactionAsync();
        await using (var lockCommand = new NpgsqlCommand(
                         $"SELECT 1 FROM inventario.assets WHERE asset_id = {assetId} FOR UPDATE", holder, holderTx))
        {
            await lockCommand.ExecuteNonQueryAsync();
        }

        var operationTask = operation switch
        {
            "start" => env.StartAsync(recordId, version),
            "complete" => env.CompleteAsync(recordId, version),
            _ => env.CancelAsync(recordId, version)
        };

        try
        {
            await WaitForBlockedBackendAsync(env.Db);

            // El registro NO debe estar bloqueado por la operación que espera al activo.
            await using var probe = await env.Db.OpenConnectionAsync();
            await using var probeTx = await probe.BeginTransactionAsync();
            await using var probeCommand = new NpgsqlCommand(
                $"SELECT 1 FROM inventario.maintenance_records WHERE maintenance_record_id = {recordId} FOR UPDATE NOWAIT",
                probe, probeTx);
            var ex = await Record.ExceptionAsync(() => probeCommand.ExecuteNonQueryAsync());
            await probeTx.RollbackAsync();

            Assert.True(ex is null,
                $"El handler de '{operation}' bloqueó el registro ANTES que el activo (orden de locks invertido): {ex?.Message}");
        }
        finally
        {
            await holderTx.RollbackAsync(); // libera al handler
        }

        var response = await operationTask;
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task WaitForBlockedBackendAsync(PostgresDatabase db)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var waiting = await db.ScalarAsync<long>("""
                SELECT count(*) FROM pg_stat_activity
                WHERE datname = current_database() AND wait_event_type = 'Lock' AND pid <> pg_backend_pid()
                """);
            if (waiting > 0)
            {
                return;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException("La operación no llegó a quedar bloqueada esperando el lock del activo.");
    }
}
