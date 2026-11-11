using System.Net;

namespace TecnmInventario.Api.Tests.Integration;

// ═══════════════════════════════════════════════════════════════════════════
// M.3: integración de mantenimiento con incidentes y estado del activo, por
// HTTP (API real + PostgreSQL). Datos ficticios.
// ═══════════════════════════════════════════════════════════════════════════

[Trait("Category", "Integration")]
public sealed class MaintenanceIntegrationTests : MaintenanceTestBase
{
    private const string InProgressMessage = "El activo tiene un mantenimiento en curso. Ciérralo o cancélalo primero.";
    private const string DisposalInProgressMessage =
        "El activo tiene un mantenimiento en curso. Ciérralo o cancélalo antes de darlo de baja.";

    public MaintenanceIntegrationTests(PostgresContainerFixture fixture)
        : base(fixture) { }

    // ── A1: resolver con mantenimiento EN_CURSO no libera ──────────────────

    [DockerFact]
    public async Task IncidenteYMantenimiento_ResolverConMantenimientoEnCurso_NoLibera()
    {
        await using var env = await CreateEnvAsync();
        var assetId = await env.InsertAssetAsync();
        var incidentId = await env.CreateIncidentAsync(assetId);
        await env.IncidentToRepairAsync(incidentId);
        var (_, _) = await StartOkAsync(env, assetId);

        var response = await env.ResolveIncidentAsync(incidentId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await MaintenanceEnv.ReadAsync(response);
        Assert.False(body.GetProperty("assetReleased").GetBoolean());
        Assert.Equal("EN_MANTENIMIENTO", body.GetProperty("assetStatusCode").GetString());
        Assert.Equal("EN_MANTENIMIENTO", await env.AssetStatusAsync(assetId));
    }

    // ── A2: incidente primero ──────────────────────────────────────────────

    [DockerFact]
    public async Task Orden_IncidentePrimero_ResolverNoLibera_CerrarMantenimientoLibera()
    {
        await using var env = await CreateEnvAsync();
        var assetId = await env.InsertAssetAsync();
        var incidentId = await env.CreateIncidentAsync(assetId);
        Assert.Equal("CON_FALLA", await env.AssetStatusAsync(assetId));
        await env.IncidentToRepairAsync(incidentId);
        Assert.Equal("EN_MANTENIMIENTO", await env.AssetStatusAsync(assetId));
        var (id, version) = await StartOkAsync(env, assetId);

        Assert.Equal(HttpStatusCode.OK, (await env.ResolveIncidentAsync(incidentId)).StatusCode);
        Assert.Equal("EN_MANTENIMIENTO", await env.AssetStatusAsync(assetId));

        var complete = await env.CompleteAsync(id, version);

        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        Assert.Equal("DISPONIBLE", await env.AssetStatusAsync(assetId));
    }

    // ── A3: mantenimiento primero ──────────────────────────────────────────

    [DockerFact]
    public async Task Orden_MantenimientoPrimero_CerrarDejaCONFALLA_ResolverLibera()
    {
        await using var env = await CreateEnvAsync();
        var assetId = await env.InsertAssetAsync();
        var (id, version) = await StartOkAsync(env, assetId);
        var incidentId = await env.CreateIncidentAsync(assetId);
        Assert.Equal("EN_MANTENIMIENTO", await env.AssetStatusAsync(assetId)); // ya fuera de servicio

        var complete = await env.CompleteAsync(id, version);

        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        Assert.Equal("CON_FALLA", await env.AssetStatusAsync(assetId));

        var resolve = await env.ResolveIncidentAsync(incidentId);

        Assert.Equal(HttpStatusCode.OK, resolve.StatusCode);
        Assert.True((await MaintenanceEnv.ReadAsync(resolve)).GetProperty("assetReleased").GetBoolean());
        Assert.Equal("DISPONIBLE", await env.AssetStatusAsync(assetId));
    }

    // ── A4 / A5 / A6 / A7: PATCH /api/assets/{id}/status ───────────────────

    [DockerTheory]
    [InlineData("DISPONIBLE")]
    [InlineData("ASIGNADO")]
    public async Task PatchStatus_SalirDeMantenimientoConRegistroEnCurso_Rechaza400(string target)
    {
        await using var env = await CreateEnvAsync();
        var assetId = await env.InsertAssetAsync();
        var (id, _) = await StartOkAsync(env, assetId);

        var response = await env.PatchStatusAsync(assetId, target);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(InProgressMessage, await response.Content.ReadAsStringAsync());
        Assert.Equal("EN_MANTENIMIENTO", await env.AssetStatusAsync(assetId));
        Assert.Equal("EN_CURSO", await env.RecordStatusAsync(id));
    }

    [DockerFact]
    public async Task PatchStatus_BajaConMantenimientoEnCurso_Rechaza400()
    {
        await using var env = await CreateEnvAsync();
        var assetId = await env.InsertAssetAsync();
        var (id, _) = await StartOkAsync(env, assetId);

        var response = await env.PatchStatusAsync(assetId, "DADO_DE_BAJA");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(DisposalInProgressMessage, await response.Content.ReadAsStringAsync());
        Assert.Equal("EN_MANTENIMIENTO", await env.AssetStatusAsync(assetId));
        Assert.Equal("EN_CURSO", await env.RecordStatusAsync(id));
    }

    [DockerFact]
    public async Task PatchStatus_BajaConProgramados_LosCancelaYAudita()
    {
        await using var env = await CreateEnvAsync();
        var assetId = await env.InsertAssetAsync();
        var first = await env.InsertScheduledAsync(assetId);
        var second = await env.InsertScheduledAsync(assetId);
        var other = await env.InsertScheduledAsync(await env.InsertAssetAsync()); // de otro activo: no se toca

        var response = await env.PatchStatusAsync(assetId, "DADO_DE_BAJA");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("DADO_DE_BAJA", await env.AssetStatusAsync(assetId));
        foreach (var id in new[] { first, second })
        {
            Assert.Equal("CANCELADO", await env.RecordStatusAsync(id));
            Assert.True(await env.Db.ScalarAsync<bool>($"""
                SELECT cancelled_at IS NOT NULL AND cancellation_reason = 'Cancelado por baja del activo.'
                FROM inventario.maintenance_records WHERE maintenance_record_id = {id}
                """));
            Assert.Equal(1, await env.AuditCountAsync("maintenance_records", id, "UPDATE"));
            Assert.Equal("CANCELADO", await env.ScalarOrNullAsync($"""
                SELECT new_data->>'Status' FROM inventario.audit_log
                WHERE table_name = 'maintenance_records' AND record_id = {id} AND action = 'UPDATE'
                """));
        }

        Assert.Equal("PROGRAMADO", await env.RecordStatusAsync(other));
        Assert.Equal(0, await env.AuditCountAsync("maintenance_records", other, "UPDATE"));
    }

    [DockerTheory]
    [InlineData("DISPONIBLE")]
    [InlineData("ASIGNADO")]
    public async Task PatchStatus_SalirDeMantenimientoSinRegistro_SigueFuncionando(string target)
    {
        await using var env = await CreateEnvAsync();
        var assetId = await env.InsertAssetAsync("EN_MANTENIMIENTO"); // puesto a mano, sin registro
        var scheduled = await env.InsertScheduledAsync(assetId);      // un PROGRAMADO no cuenta

        var response = await env.PatchStatusAsync(assetId, target);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(target, await env.AssetStatusAsync(assetId));
        Assert.Equal("PROGRAMADO", await env.RecordStatusAsync(scheduled));
    }

    // ── A8: regresión de ResolveIncident sin mantenimiento ─────────────────

    [DockerTheory]
    [InlineData("CON_FALLA", false, "DISPONIBLE", true)]
    [InlineData("CON_FALLA", true, "ASIGNADO", true)]
    [InlineData("EN_MANTENIMIENTO", false, "DISPONIBLE", true)]
    [InlineData("EN_MANTENIMIENTO", true, "ASIGNADO", true)]
    public async Task ResolveIncident_SinMantenimiento_MismosEstadosQueAntes(
        string startStatus, bool withAssignment, string expected, bool released)
    {
        await using var env = await CreateEnvAsync();
        var assetId = await env.InsertAssetAsync(startStatus);
        if (withAssignment)
        {
            await env.InsertAssignmentAsync(assetId);
        }

        var incidentId = await env.InsertIncidentAsync(assetId, "ABIERTO");

        var response = await env.ResolveIncidentAsync(incidentId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await MaintenanceEnv.ReadAsync(response);
        Assert.Equal(expected, body.GetProperty("assetStatusCode").GetString());
        Assert.Equal(released, body.GetProperty("assetReleased").GetBoolean());
        Assert.Equal(expected, await env.AssetStatusAsync(assetId));
        // CON_FALLA pasa por EN_MANTENIMIENTO (dos cambios); EN_MANTENIMIENTO, uno.
        Assert.Equal(startStatus == "CON_FALLA" ? 2 : 1, await env.AuditCountAsync("assets", assetId, "UPDATE"));
    }

    [DockerTheory]
    [InlineData("CON_FALLA", "ABIERTO")]
    [InlineData("EN_MANTENIMIENTO", "EN_REPARACION")]
    [InlineData("DISPONIBLE", "ABIERTO")]
    public async Task ResolveIncident_ConOtroIncidenteAbierto_NoCambiaElActivo(string startStatus, string otherStatus)
    {
        await using var env = await CreateEnvAsync();
        var assetId = await env.InsertAssetAsync(startStatus);
        var incidentId = await env.InsertIncidentAsync(assetId, "ABIERTO");
        await env.InsertIncidentAsync(assetId, otherStatus);

        var response = await env.ResolveIncidentAsync(incidentId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await MaintenanceEnv.ReadAsync(response);
        Assert.False(body.GetProperty("assetReleased").GetBoolean());
        Assert.Equal(1, body.GetProperty("openIncidentsRemaining").GetInt32());
        Assert.Equal(startStatus, await env.AssetStatusAsync(assetId));
    }

    // ── A9: carrera cerrar mantenimiento ‖ resolver incidente ──────────────

    [DockerFact]
    public async Task Carrera_CerrarMantenimiento_Y_ResolverIncidente_SinDeadlockNiError500()
    {
        await using var env = await CreateEnvAsync();
        var failures = new List<string>();

        for (var iteration = 1; iteration <= 12; iteration++)
        {
            var assetId = await env.InsertAssetAsync();
            var incidentId = await env.CreateIncidentAsync(assetId);
            await env.IncidentToRepairAsync(incidentId);
            var (id, version) = await StartOkAsync(env, assetId);
            var gate = new TaskCompletionSource();

            var complete = Task.Run(async () =>
            {
                await gate.Task;
                return (await env.CompleteAsync(id, version)).StatusCode;
            });
            var resolve = Task.Run(async () =>
            {
                await gate.Task;
                return (await env.ResolveIncidentAsync(incidentId)).StatusCode;
            });
            gate.SetResult();
            var codes = await Task.WhenAll(complete, resolve);

            // Con las dos operaciones terminadas ya no queda motivo para tener el
            // activo fuera de servicio: DISPONIBLE, sea cual sea el orden.
            var status = await env.AssetStatusAsync(assetId);
            if (codes.Any(c => c != HttpStatusCode.OK) || status != "DISPONIBLE")
            {
                failures.Add($"iteración {iteration}: códigos [{string.Join(", ", codes)}], activo={status}");
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    // ── A10: movimiento y asignación durante el mantenimiento (riesgo 6) ───

    [DockerFact]
    public async Task MovimientoYAsignacion_BloqueadosConMantenimientoEnCurso_PermitidosTrasCerrarlo()
    {
        await using var env = await CreateEnvAsync();
        var assetId = await env.InsertAssetAsync();
        var (id, version) = await StartOkAsync(env, assetId);

        var moveDuring = await env.MoveAsync(assetId);
        var assignDuring = await env.AssignAsync(assetId);

        Assert.Equal(HttpStatusCode.BadRequest, moveDuring.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, assignDuring.StatusCode);
        Assert.Equal("EN_MANTENIMIENTO", await env.AssetStatusAsync(assetId));

        Assert.Equal(HttpStatusCode.OK, (await env.CompleteAsync(id, version)).StatusCode);

        Assert.Equal(HttpStatusCode.Created, (await env.MoveAsync(assetId)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await env.AssignAsync(assetId)).StatusCode);
        Assert.Equal("ASIGNADO", await env.AssetStatusAsync(assetId));
    }

    // ── Soportes ───────────────────────────────────────────────────────────

    private static async Task<(long Id, int Version)> StartOkAsync(MaintenanceEnv env, long assetId)
    {
        var response = await env.StartDirectAsync(assetId);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await MaintenanceEnv.ReadAsync(response);
        return (body.GetProperty("maintenanceRecordId").GetInt64(), body.GetProperty("version").GetInt32());
    }
}
