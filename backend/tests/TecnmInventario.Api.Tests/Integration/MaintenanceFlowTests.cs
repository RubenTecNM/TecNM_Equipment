using System.Net;

namespace TecnmInventario.Api.Tests.Integration;

// ═══════════════════════════════════════════════════════════════════════════
// M.2: núcleo del flujo de mantenimiento por HTTP (API real + PostgreSQL).
// programar -> iniciar -> cerrar / cancelar. Datos ficticios.
// ═══════════════════════════════════════════════════════════════════════════

[Trait("Category", "Integration")]
public sealed class MaintenanceFlowTests : MaintenanceTestBase
{
    public MaintenanceFlowTests(PostgresContainerFixture fixture)
        : base(fixture) { }

    // ── A1: ScheduleMaintenance ────────────────────────────────────────────

    [DockerFact]
    public async Task Schedule_Valido_201_Programado_ActivoSinCambios()
    {
        await using var env = await CreateEnvAsync();
        var assetId = await env.InsertAssetAsync();
        var assetVersion = await env.AssetVersionAsync(assetId);

        var response = await env.ScheduleAsync(assetId);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await MaintenanceEnv.ReadAsync(response);
        var id = body.GetProperty("maintenanceRecordId").GetInt64();
        Assert.EndsWith($"/api/maintenance/{id}", response.Headers.Location?.ToString());
        Assert.Equal("PROGRAMADO", body.GetProperty("status").GetString());
        Assert.Equal(assetId, body.GetProperty("assetId").GetInt64());
        Assert.Equal("DISPONIBLE", body.GetProperty("assetStatusCode").GetString());
        Assert.Equal(1, body.GetProperty("version").GetInt32());

        Assert.Equal("PROGRAMADO", await env.RecordStatusAsync(id));
        Assert.Equal("DISPONIBLE", await env.AssetStatusAsync(assetId));
        Assert.Equal(assetVersion, await env.AssetVersionAsync(assetId)); // el activo no se tocó
        Assert.Equal(1, await env.AuditCountAsync("maintenance_records", id, "INSERT"));
    }

    [DockerFact]
    public async Task Schedule_ConFechaPasada_EsValido()
    {
        await using var env = await CreateEnvAsync();
        var assetId = await env.InsertAssetAsync();

        var response = await env.PostAsync("/api/maintenance", new
        {
            assetId,
            maintenanceTypeId = env.TypeId,
            scheduledAt = "2020-01-15T08:00:00Z"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [DockerTheory]
    [InlineData("activo_dado_de_baja")]
    [InlineData("activo_borrado")]
    [InlineData("activo_inexistente")]
    [InlineData("tipo_borrado")]
    [InlineData("tecnico_sin_permiso")]
    [InlineData("tecnico_inactivo")]
    [InlineData("sin_fecha")]
    public async Task Schedule_Rechaza400(string caso)
    {
        await using var env = await CreateEnvAsync();
        var assetId = await env.InsertAssetAsync();
        long? performedBy = null;
        long typeId = env.TypeId;
        object? scheduledAt = "2026-12-01T09:00:00Z";
        var targetAsset = assetId;

        switch (caso)
        {
            case "activo_dado_de_baja":
                await env.SetAssetStatusAsync(assetId, "DADO_DE_BAJA");
                break;
            case "activo_borrado":
                await env.Db.ExecuteAsync($"UPDATE inventario.assets SET is_deleted = true WHERE asset_id = {assetId}");
                break;
            case "activo_inexistente":
                targetAsset = 987_654;
                break;
            case "tipo_borrado":
                await env.Db.ExecuteAsync(
                    $"UPDATE inventario.maintenance_types SET is_deleted = true WHERE maintenance_type_id = {typeId}");
                break;
            case "tecnico_sin_permiso":
                // RESPONSABLE_AREA solo tiene maintenance.view, no maintenance.manage.
                performedBy = (await env.CreateUserAsync("m2_responsable", "RESPONSABLE_AREA")).UserId;
                break;
            case "tecnico_inactivo":
                var (_, tecnicoId) = await env.CreateUserAsync("m2_tecnico_inactivo", "TECNICO");
                await env.Db.ExecuteAsync($"UPDATE inventario.app_users SET status = 'inactivo' WHERE user_id = {tecnicoId}");
                performedBy = tecnicoId;
                break;
            case "sin_fecha":
                scheduledAt = null;
                break;
        }

        var response = await env.PostAsync("/api/maintenance", new
        {
            assetId = targetAsset,
            maintenanceTypeId = typeId,
            scheduledAt,
            performedBy
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await env.Db.CountAsync("maintenance_records", $"asset_id = {assetId}"));
    }

    [DockerFact]
    public async Task Schedule_ConTecnicoValido_GuardaPerformedBy()
    {
        await using var env = await CreateEnvAsync();
        var assetId = await env.InsertAssetAsync();
        var (_, tecnicoId) = await env.CreateUserAsync("m2_tecnico", "TECNICO");

        var response = await env.PostAsync("/api/maintenance", new
        {
            assetId,
            maintenanceTypeId = env.TypeId,
            scheduledAt = "2026-12-01T09:00:00Z",
            performedBy = tecnicoId
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var id = (await MaintenanceEnv.ReadAsync(response)).GetProperty("maintenanceRecordId").GetInt64();
        Assert.Equal(tecnicoId, await env.Db.ScalarAsync<long>(
            $"SELECT performed_by FROM inventario.maintenance_records WHERE maintenance_record_id = {id}"));
    }

    [DockerTheory]
    [InlineData("USUARIO_GENERAL")]
    [InlineData("RESPONSABLE_AREA")]
    public async Task EscrituraSinPermiso_Devuelve403(string roleCode)
    {
        await using var env = await CreateEnvAsync();
        var assetId = await env.InsertAssetAsync();
        var recordId = await env.InsertScheduledAsync(assetId);
        var (token, _) = await env.CreateUserAsync($"m2_{roleCode.ToLowerInvariant()}", roleCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await env.ScheduleAsync(assetId, token: token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await env.StartDirectAsync(assetId, token: token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await env.StartAsync(recordId, 1, token: token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await env.CompleteAsync(recordId, 1, token: token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await env.CancelAsync(recordId, 1, token: token)).StatusCode);
        Assert.Equal("PROGRAMADO", await env.RecordStatusAsync(recordId));
    }

    // ── A2: Start desde un registro programado ─────────────────────────────

    [DockerFact]
    public async Task Start_DesdeProgramado_ActivoDisponible_PasaAEnMantenimiento()
    {
        await using var env = await CreateEnvAsync();
        var assetId = await env.InsertAssetAsync();
        var id = await ScheduleOkAsync(env, assetId);

        var response = await env.StartAsync(id, expectedVersion: 1);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await MaintenanceEnv.ReadAsync(response);
        Assert.Equal("EN_CURSO", body.GetProperty("status").GetString());
        Assert.Equal("EN_MANTENIMIENTO", body.GetProperty("assetStatusCode").GetString());
        Assert.True(body.GetProperty("version").GetInt32() > 1);

        Assert.Equal("EN_MANTENIMIENTO", await env.AssetStatusAsync(assetId));
        Assert.Equal("DISPONIBLE", await env.RecordStatusColumnAsync(id, "status_before_id"));
        Assert.True(await env.Db.ScalarAsync<bool>(
            $"SELECT started_at IS NOT NULL FROM inventario.maintenance_records WHERE maintenance_record_id = {id}"));
        Assert.Equal(env.AdminId, await env.Db.ScalarAsync<long>(
            $"SELECT performed_by FROM inventario.maintenance_records WHERE maintenance_record_id = {id}"));

        // Auditoría: el registro (UPDATE) y el cambio de estado del activo.
        Assert.Equal(1, await env.AuditCountAsync("maintenance_records", id, "UPDATE"));
        Assert.Equal("EN_CURSO", await env.ScalarOrNullAsync($"""
            SELECT new_data->>'Status' FROM inventario.audit_log
            WHERE table_name = 'maintenance_records' AND record_id = {id} AND action = 'UPDATE'
            """));
        Assert.Equal(1, await env.AuditCountAsync("assets", assetId, "UPDATE"));
    }

    [DockerTheory]
    [InlineData("EN_CURSO")]
    [InlineData("COMPLETADO")]
    [InlineData("CANCELADO")]
    public async Task Start_DesdeUnEstadoQueNoEsProgramado_Rechaza400(string status)
    {
        await using var env = await CreateEnvAsync();
        var assetId = await env.InsertAssetAsync();
        var id = await env.Db.ScalarAsync<long>(status switch
        {
            "EN_CURSO" => $"INSERT INTO inventario.maintenance_records (asset_id, maintenance_type_id, status, started_at) VALUES ({assetId}, {env.TypeId}, 'EN_CURSO', now()) RETURNING maintenance_record_id",
            "COMPLETADO" => $"INSERT INTO inventario.maintenance_records (asset_id, maintenance_type_id, status, started_at, performed_at) VALUES ({assetId}, {env.TypeId}, 'COMPLETADO', now(), now()) RETURNING maintenance_record_id",
            _ => $"INSERT INTO inventario.maintenance_records (asset_id, maintenance_type_id, status, cancelled_at) VALUES ({assetId}, {env.TypeId}, 'CANCELADO', now()) RETURNING maintenance_record_id",
        });

        var response = await env.StartAsync(id, expectedVersion: 1);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(status, await env.RecordStatusAsync(id));
        Assert.Equal("DISPONIBLE", await env.AssetStatusAsync(assetId));
    }

    [DockerFact]
    public async Task Start_DeUnRegistroInexistente_Devuelve404()
    {
        await using var env = await CreateEnvAsync();

        var response = await env.StartAsync(987_654, expectedVersion: 1);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── A3: Start directo con incidente ────────────────────────────────────

    [DockerFact]
    public async Task StartDirecto_ActivoConFalla_ConIncidenteDelMismoActivo_EnCurso()
    {
        await using var env = await CreateEnvAsync();
        var assetId = await env.InsertAssetAsync("CON_FALLA");
        var incidentId = await env.InsertIncidentAsync(assetId, "ABIERTO");

        var response = await env.StartDirectAsync(assetId, incidentId);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await MaintenanceEnv.ReadAsync(response);
        var id = body.GetProperty("maintenanceRecordId").GetInt64();
        Assert.EndsWith($"/api/maintenance/{id}", response.Headers.Location?.ToString());
        Assert.Equal("EN_CURSO", body.GetProperty("status").GetString());
        Assert.Equal("EN_MANTENIMIENTO", body.GetProperty("assetStatusCode").GetString());

        Assert.Equal("EN_MANTENIMIENTO", await env.AssetStatusAsync(assetId));
        Assert.Equal("CON_FALLA", await env.RecordStatusColumnAsync(id, "status_before_id"));
        Assert.Equal(incidentId, await env.Db.ScalarAsync<long>(
            $"SELECT incident_id FROM inventario.maintenance_records WHERE maintenance_record_id = {id}"));
        Assert.Equal(1, await env.AuditCountAsync("maintenance_records", id, "INSERT"));
    }

    [DockerTheory]
    [InlineData("otro_activo", "ABIERTO")]
    [InlineData("mismo_activo", "CERRADO")]
    [InlineData("mismo_activo", "RESUELTO")]
    [InlineData("inexistente", "ABIERTO")]
    public async Task StartDirecto_ConIncidenteInvalido_Rechaza400(string caso, string incidentStatus)
    {
        await using var env = await CreateEnvAsync();
        var assetId = await env.InsertAssetAsync("CON_FALLA");
        var otherAssetId = await env.InsertAssetAsync("CON_FALLA");

        var incidentId = caso switch
        {
            "otro_activo" => await env.InsertIncidentAsync(otherAssetId, incidentStatus),
            "mismo_activo" => await env.InsertIncidentAsync(assetId, incidentStatus),
            _ => 987_654
        };

        var response = await env.StartDirectAsync(assetId, incidentId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await env.Db.CountAsync("maintenance_records", $"asset_id = {assetId}"));
        Assert.Equal("CON_FALLA", await env.AssetStatusAsync(assetId));
    }

    [DockerFact]
    public async Task StartDesdeProgramado_ConIncidenteDeOtroActivo_Rechaza400()
    {
        await using var env = await CreateEnvAsync();
        var assetId = await env.InsertAssetAsync("CON_FALLA");
        var otherAssetId = await env.InsertAssetAsync("CON_FALLA");
        var incidentId = await env.InsertIncidentAsync(otherAssetId, "ABIERTO");
        var id = await env.InsertScheduledAsync(assetId);

        var response = await env.StartAsync(id, 1, incidentId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("PROGRAMADO", await env.RecordStatusAsync(id));
        Assert.Equal("CON_FALLA", await env.AssetStatusAsync(assetId));
    }

    // Start sobre un activo DADO_DE_BAJA, por los dos caminos.
    [DockerFact]
    public async Task Start_ActivoDadoDeBaja_Rechaza400_PorLosDosEndpoints()
    {
        await using var env = await CreateEnvAsync();
        var assetId = await env.InsertAssetAsync();
        var id = await env.InsertScheduledAsync(assetId);
        await env.SetAssetStatusAsync(assetId, "DADO_DE_BAJA");

        var direct = await env.StartDirectAsync(assetId);
        var fromScheduled = await env.StartAsync(id, 1);

        Assert.Equal(HttpStatusCode.BadRequest, direct.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, fromScheduled.StatusCode);
        // El MENSAJE distingue la guarda explícita del handler de la de la matriz
        // de IAssetStatusChanger (que también rechazaría DADO_DE_BAJA -> EN_MANTENIMIENTO
        // con otro texto): sin esta aserción quitar la guarda no se notaría.
        Assert.Contains("dado de baja", await direct.Content.ReadAsStringAsync());
        Assert.Contains("dado de baja", await fromScheduled.Content.ReadAsStringAsync());
        Assert.Equal("PROGRAMADO", await env.RecordStatusAsync(id));
        Assert.Equal("DADO_DE_BAJA", await env.AssetStatusAsync(assetId));
        Assert.Equal(1, await env.Db.CountAsync("maintenance_records", $"asset_id = {assetId}"));
    }

    [DockerFact]
    public async Task StartDirecto_ConTecnicoSinPermiso_Rechaza400()
    {
        await using var env = await CreateEnvAsync();
        var assetId = await env.InsertAssetAsync();
        var (_, responsableId) = await env.CreateUserAsync("m2_resp_directo", "RESPONSABLE_AREA");

        var response = await env.StartDirectAsync(assetId, performedBy: responsableId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("DISPONIBLE", await env.AssetStatusAsync(assetId));
    }

    // ── A4: Start con el activo ya EN_MANTENIMIENTO ────────────────────────

    [DockerFact]
    public async Task Start_ActivoYaEnMantenimiento_Acepta_YNoCambiaElEstadoDelActivo()
    {
        await using var env = await CreateEnvAsync();
        var assetId = await env.InsertAssetAsync("EN_MANTENIMIENTO"); // p. ej. por un incidente EN_REPARACION
        await env.InsertIncidentAsync(assetId, "EN_REPARACION");
        var assetVersion = await env.AssetVersionAsync(assetId);

        var response = await env.StartDirectAsync(assetId);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var id = (await MaintenanceEnv.ReadAsync(response)).GetProperty("maintenanceRecordId").GetInt64();
        Assert.Equal("EN_MANTENIMIENTO", await env.AssetStatusAsync(assetId));
        Assert.Equal(assetVersion, await env.AssetVersionAsync(assetId)); // no se tocó la fila del activo
        Assert.Equal("EN_MANTENIMIENTO", await env.RecordStatusColumnAsync(id, "status_before_id"));
        Assert.Equal(0, await env.AuditCountAsync("assets", assetId, "UPDATE"));
    }

    // ── A5 (parte secuencial): un solo EN_CURSO por activo ─────────────────

    [DockerFact]
    public async Task Start_SegundoEnElMismoActivo_ConUnoEnCurso_Rechaza400()
    {
        await using var env = await CreateEnvAsync();
        var assetId = await env.InsertAssetAsync();
        var scheduledId = await env.InsertScheduledAsync(assetId);

        Assert.Equal(HttpStatusCode.Created, (await env.StartDirectAsync(assetId)).StatusCode);

        var second = await env.StartDirectAsync(assetId);
        var fromScheduled = await env.StartAsync(scheduledId, 1);

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        Assert.Contains("ya tiene un mantenimiento en curso", await second.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, fromScheduled.StatusCode);
        Assert.Equal("PROGRAMADO", await env.RecordStatusAsync(scheduledId));
        Assert.Equal(1, await env.Db.CountAsync("maintenance_records", $"asset_id = {assetId} AND status = 'EN_CURSO'"));
    }

    // ── A6: Complete ───────────────────────────────────────────────────────

    [DockerFact]
    public async Task Complete_SinAsignacion_Libera_Disponible_YGuardaLosCampos()
    {
        await using var env = await CreateEnvAsync();
        var assetId = await env.InsertAssetAsync();
        var (id, version) = await StartDirectOkAsync(env, assetId);

        var response = await env.CompleteAsync(id, version, new
        {
            expectedVersion = version,
            description = "Limpieza y cambio de pasta térmica",
            partsUsed = "Pasta térmica 1 pza",
            cost = 120.50m,
            nextMaintenanceAt = "2099-06-01T08:00:00Z"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await MaintenanceEnv.ReadAsync(response);
        Assert.Equal("COMPLETADO", body.GetProperty("status").GetString());
        Assert.Equal("DISPONIBLE", body.GetProperty("assetStatusCode").GetString());
        Assert.True(body.GetProperty("released").GetBoolean());
        Assert.Equal(0, body.GetProperty("openIncidents").GetInt32());

        Assert.Equal("DISPONIBLE", await env.AssetStatusAsync(assetId));
        Assert.Equal("DISPONIBLE", await env.RecordStatusColumnAsync(id, "status_after_id"));
        Assert.True(await env.Db.ScalarAsync<bool>($"""
            SELECT performed_at IS NOT NULL AND cost = 120.50 AND parts_used = 'Pasta térmica 1 pza'
                   AND next_maintenance_at IS NOT NULL AND description = 'Limpieza y cambio de pasta térmica'
            FROM inventario.maintenance_records WHERE maintenance_record_id = {id}
            """));
        Assert.Equal(1, await env.AuditCountAsync("maintenance_records", id, "UPDATE"));
    }

    [DockerFact]
    public async Task Complete_ConAsignacionVigente_Libera_Asignado()
    {
        await using var env = await CreateEnvAsync();
        var assetId = await env.InsertAssetAsync("ASIGNADO");
        await env.InsertAssignmentAsync(assetId);
        var (id, version) = await StartDirectOkAsync(env, assetId);
        Assert.Equal("EN_MANTENIMIENTO", await env.AssetStatusAsync(assetId));

        var response = await env.CompleteAsync(id, version);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await MaintenanceEnv.ReadAsync(response);
        Assert.Equal("ASIGNADO", body.GetProperty("assetStatusCode").GetString());
        Assert.True(body.GetProperty("released").GetBoolean());
        Assert.Equal("ASIGNADO", await env.AssetStatusAsync(assetId));
        Assert.Equal("ASIGNADO", await env.RecordStatusColumnAsync(id, "status_after_id"));
        Assert.Equal("ASIGNADO", await env.RecordStatusColumnAsync(id, "status_before_id"));
    }

    [DockerTheory]
    [InlineData("fecha_pasada")]
    [InlineData("costo_negativo")]
    [InlineData("costo_desbordado")]
    [InlineData("version_cero")]
    public async Task Complete_ConDatosInvalidos_Rechaza400_SinCambios(string caso)
    {
        await using var env = await CreateEnvAsync();
        var assetId = await env.InsertAssetAsync();
        var (id, version) = await StartDirectOkAsync(env, assetId);

        object body = caso switch
        {
            "fecha_pasada" => new { expectedVersion = version, nextMaintenanceAt = "2020-01-01T00:00:00Z" },
            "costo_negativo" => new { expectedVersion = version, cost = -1 },
            "costo_desbordado" => new { expectedVersion = version, cost = 9999999999999.99m },
            _ => new { expectedVersion = 0 }
        };

        var response = await env.CompleteAsync(id, version, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("EN_CURSO", await env.RecordStatusAsync(id));
        Assert.Equal("EN_MANTENIMIENTO", await env.AssetStatusAsync(assetId));
    }

    [DockerFact]
    public async Task Complete_DeUnRegistroProgramado_Rechaza400()
    {
        await using var env = await CreateEnvAsync();
        var assetId = await env.InsertAssetAsync();
        var id = await env.InsertScheduledAsync(assetId);

        var response = await env.CompleteAsync(id, 1);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("PROGRAMADO", await env.RecordStatusAsync(id));
    }

    // ── A7: Complete con incidentes abiertos (no se libera) ────────────────

    [DockerTheory]
    [InlineData("EN_REPARACION", null, "EN_MANTENIMIENTO", 1)]
    [InlineData("ABIERTO", null, "CON_FALLA", 1)]
    [InlineData("EN_DIAGNOSTICO", null, "CON_FALLA", 1)]
    [InlineData("ABIERTO", "EN_REPARACION", "EN_MANTENIMIENTO", 2)] // uno en reparación manda
    [InlineData("RESUELTO", null, "DISPONIBLE", 0)] // los resueltos no cuentan
    public async Task Complete_ConIncidentesAbiertos_NoLiberaElActivo(
        string firstIncident, string? secondIncident, string expectedAsset, int expectedOpen)
    {
        await using var env = await CreateEnvAsync();
        var assetId = await env.InsertAssetAsync();
        var (id, version) = await StartDirectOkAsync(env, assetId);

        // Los incidentes se reportan durante el mantenimiento.
        await env.InsertIncidentAsync(assetId, firstIncident);
        if (secondIncident is not null)
        {
            await env.InsertIncidentAsync(assetId, secondIncident);
        }

        var response = await env.CompleteAsync(id, version);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await MaintenanceEnv.ReadAsync(response);
        Assert.Equal("COMPLETADO", body.GetProperty("status").GetString());
        Assert.Equal(expectedAsset, body.GetProperty("assetStatusCode").GetString());
        Assert.Equal(expectedOpen, body.GetProperty("openIncidents").GetInt32());
        Assert.Equal(expectedAsset == "DISPONIBLE", body.GetProperty("released").GetBoolean());
        Assert.Equal(expectedAsset, await env.AssetStatusAsync(assetId));
        Assert.Equal(expectedAsset, await env.RecordStatusColumnAsync(id, "status_after_id"));
    }

    // ── A8: Cancel ─────────────────────────────────────────────────────────

    [DockerFact]
    public async Task Cancel_Programado_ActivoIntacto()
    {
        await using var env = await CreateEnvAsync();
        var assetId = await env.InsertAssetAsync();
        var id = await env.InsertScheduledAsync(assetId);
        var assetVersion = await env.AssetVersionAsync(assetId);

        var response = await env.CancelAsync(id, 1, "Ya no se requiere");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await MaintenanceEnv.ReadAsync(response);
        Assert.Equal("CANCELADO", body.GetProperty("status").GetString());
        Assert.False(body.GetProperty("released").GetBoolean());
        Assert.Equal("DISPONIBLE", body.GetProperty("assetStatusCode").GetString());

        Assert.Equal("CANCELADO", await env.RecordStatusAsync(id));
        Assert.Equal("DISPONIBLE", await env.AssetStatusAsync(assetId));
        Assert.Equal(assetVersion, await env.AssetVersionAsync(assetId));
        Assert.True(await env.Db.ScalarAsync<bool>($"""
            SELECT cancelled_at IS NOT NULL AND cancellation_reason = 'Ya no se requiere' AND status_after_id IS NULL
            FROM inventario.maintenance_records WHERE maintenance_record_id = {id}
            """));
        Assert.Equal(1, await env.AuditCountAsync("maintenance_records", id, "UPDATE"));
    }

    [DockerFact]
    public async Task Cancel_EnCurso_LiberaComoComplete()
    {
        await using var env = await CreateEnvAsync();
        var assetId = await env.InsertAssetAsync();
        var (id, version) = await StartDirectOkAsync(env, assetId);

        var response = await env.CancelAsync(id, version, "Falta de refacciones");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await MaintenanceEnv.ReadAsync(response);
        Assert.Equal("CANCELADO", body.GetProperty("status").GetString());
        Assert.Equal("DISPONIBLE", body.GetProperty("assetStatusCode").GetString());
        Assert.True(body.GetProperty("released").GetBoolean());
        Assert.Equal("DISPONIBLE", await env.AssetStatusAsync(assetId));
        Assert.Equal("DISPONIBLE", await env.RecordStatusColumnAsync(id, "status_after_id"));
    }

    [DockerFact]
    public async Task Cancel_EnCurso_ConIncidenteEnReparacion_NoLibera()
    {
        await using var env = await CreateEnvAsync();
        var assetId = await env.InsertAssetAsync();
        var (id, version) = await StartDirectOkAsync(env, assetId);
        await env.InsertIncidentAsync(assetId, "EN_REPARACION");

        var response = await env.CancelAsync(id, version);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await MaintenanceEnv.ReadAsync(response);
        Assert.False(body.GetProperty("released").GetBoolean());
        Assert.Equal(1, body.GetProperty("openIncidents").GetInt32());
        Assert.Equal("EN_MANTENIMIENTO", await env.AssetStatusAsync(assetId));
    }

    [DockerTheory]
    [InlineData("COMPLETADO")]
    [InlineData("CANCELADO")]
    public async Task Cancel_DeUnEstadoTerminal_Rechaza400(string status)
    {
        await using var env = await CreateEnvAsync();
        var assetId = await env.InsertAssetAsync();
        var (id, version) = await StartDirectOkAsync(env, assetId);
        var response0 = status == "COMPLETADO"
            ? await env.CompleteAsync(id, version)
            : await env.CancelAsync(id, version);
        Assert.Equal(HttpStatusCode.OK, response0.StatusCode);
        var newVersion = await env.RecordVersionAsync(id);

        var response = await env.CancelAsync(id, newVersion, "Otra vez");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(status, await env.RecordStatusAsync(id));
    }

    [DockerTheory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Cancel_SinReason_Rechaza400(string? reason)
    {
        await using var env = await CreateEnvAsync();
        var assetId = await env.InsertAssetAsync();
        var id = await env.InsertScheduledAsync(assetId);

        var response = await env.CancelAsync(id, 1, reason);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("PROGRAMADO", await env.RecordStatusAsync(id));
    }

    [DockerFact]
    public async Task Cancel_ReasonDeMasDe500Caracteres_Rechaza400()
    {
        await using var env = await CreateEnvAsync();
        var assetId = await env.InsertAssetAsync();
        var id = await env.InsertScheduledAsync(assetId);

        var response = await env.CancelAsync(id, 1, new string('x', 501));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("PROGRAMADO", await env.RecordStatusAsync(id));
    }

    // ── A9: ExpectedVersion vieja -> 409 y nada cambia ─────────────────────

    [DockerFact]
    public async Task Start_ConExpectedVersionVieja_409_YNadaCambia()
    {
        await using var env = await CreateEnvAsync();
        var assetId = await env.InsertAssetAsync();
        var id = await env.InsertScheduledAsync(assetId);
        await env.Db.ExecuteAsync(
            $"UPDATE inventario.maintenance_records SET description = 'editado' WHERE maintenance_record_id = {id}");
        var assetVersion = await env.AssetVersionAsync(assetId);
        var auditBefore = await env.Db.CountAsync("audit_log");

        var response = await env.StartAsync(id, expectedVersion: 1);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("PROGRAMADO", await env.RecordStatusAsync(id));
        Assert.Equal("DISPONIBLE", await env.AssetStatusAsync(assetId));
        Assert.Equal(assetVersion, await env.AssetVersionAsync(assetId));
        Assert.Equal(auditBefore, await env.Db.CountAsync("audit_log"));
    }

    [DockerFact]
    public async Task Complete_ConExpectedVersionVieja_409_YNadaCambia()
    {
        await using var env = await CreateEnvAsync();
        var assetId = await env.InsertAssetAsync();
        var (id, version) = await StartDirectOkAsync(env, assetId);
        await env.Db.ExecuteAsync(
            $"UPDATE inventario.maintenance_records SET description = 'editado' WHERE maintenance_record_id = {id}");
        var assetVersion = await env.AssetVersionAsync(assetId);
        var auditBefore = await env.Db.CountAsync("audit_log");

        var response = await env.CompleteAsync(id, version);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("EN_CURSO", await env.RecordStatusAsync(id));
        Assert.Equal("EN_MANTENIMIENTO", await env.AssetStatusAsync(assetId));
        Assert.Equal(assetVersion, await env.AssetVersionAsync(assetId));
        Assert.Equal(auditBefore, await env.Db.CountAsync("audit_log"));
    }

    [DockerFact]
    public async Task Cancel_ConExpectedVersionVieja_409_YNadaCambia()
    {
        await using var env = await CreateEnvAsync();
        var assetId = await env.InsertAssetAsync();
        var (id, version) = await StartDirectOkAsync(env, assetId);
        await env.Db.ExecuteAsync(
            $"UPDATE inventario.maintenance_records SET description = 'editado' WHERE maintenance_record_id = {id}");
        var assetVersion = await env.AssetVersionAsync(assetId);
        var auditBefore = await env.Db.CountAsync("audit_log");

        var response = await env.CancelAsync(id, version);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("EN_CURSO", await env.RecordStatusAsync(id));
        Assert.Equal("EN_MANTENIMIENTO", await env.AssetStatusAsync(assetId));
        Assert.Equal(assetVersion, await env.AssetVersionAsync(assetId));
        Assert.Equal(auditBefore, await env.Db.CountAsync("audit_log"));
    }

    // ── A10: asignar -> iniciar -> devolver -> cerrar ──────────────────────

    [DockerFact]
    public async Task Asignar_Start_DevolverAsignacion_Complete_DejaElActivoDisponible()
    {
        await using var env = await CreateEnvAsync();
        var assetId = await env.InsertAssetAsync();

        // Asignar por la API real.
        var assignment = await env.SendAsync(HttpMethod.Post,
            $"/api/assets/{assetId}/assignments?expectedVersion={await env.AssetVersionAsync(assetId)}",
            new { assignedTo = env.AdminId, assignmentTypeId = await env.Db.ScalarAsync<long>(
                "SELECT assignment_type_id FROM inventario.assignment_types WHERE code = 'PERSONAL'") });
        Assert.Equal(HttpStatusCode.Created, assignment.StatusCode);
        var assignmentId = (await MaintenanceEnv.ReadAsync(assignment)).GetProperty("assetAssignmentId").GetInt64();
        Assert.Equal("ASIGNADO", await env.AssetStatusAsync(assetId));

        var (id, version) = await StartDirectOkAsync(env, assetId);
        Assert.Equal("EN_MANTENIMIENTO", await env.AssetStatusAsync(assetId));

        // Devolver la asignación DURANTE el mantenimiento: el estado no cambia.
        var returned = await env.SendAsync(HttpMethod.Post,
            $"/api/assets/{assetId}/assignments/{assignmentId}/return?expectedVersion={await env.AssetVersionAsync(assetId)}");
        Assert.Equal(HttpStatusCode.OK, returned.StatusCode);
        Assert.Equal("EN_MANTENIMIENTO", await env.AssetStatusAsync(assetId));

        // Al cerrar se evalúa la asignación vigente: ya no hay, queda DISPONIBLE.
        var response = await env.CompleteAsync(id, version);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await MaintenanceEnv.ReadAsync(response);
        Assert.Equal("DISPONIBLE", body.GetProperty("assetStatusCode").GetString());
        Assert.Equal("DISPONIBLE", await env.AssetStatusAsync(assetId));
        Assert.Equal("ASIGNADO", await env.RecordStatusColumnAsync(id, "status_before_id"));
        Assert.Equal("DISPONIBLE", await env.RecordStatusColumnAsync(id, "status_after_id"));
    }

    // ── A11: activo borrado lógicamente entre Schedule y Start ─────────────

    [DockerFact]
    public async Task Start_ActivoBorradoEntreScheduleYStart_Rechaza400()
    {
        await using var env = await CreateEnvAsync();
        var assetId = await env.InsertAssetAsync();
        var id = await ScheduleOkAsync(env, assetId);
        await env.Db.ExecuteAsync($"UPDATE inventario.assets SET is_deleted = true WHERE asset_id = {assetId}");

        var response = await env.StartAsync(id, expectedVersion: 1);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("eliminado", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("PROGRAMADO", await env.RecordStatusAsync(id));
        Assert.Equal("DISPONIBLE", await env.AssetStatusAsync(assetId));
    }

    [DockerFact]
    public async Task CompleteYCancel_ConActivoBorrado_Rechazan400()
    {
        await using var env = await CreateEnvAsync();
        var assetId = await env.InsertAssetAsync();
        var (id, version) = await StartDirectOkAsync(env, assetId);
        await env.Db.ExecuteAsync($"UPDATE inventario.assets SET is_deleted = true WHERE asset_id = {assetId}");

        var complete = await env.CompleteAsync(id, version);
        var cancel = await env.CancelAsync(id, version);

        Assert.Equal(HttpStatusCode.BadRequest, complete.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, cancel.StatusCode);
        Assert.Equal("EN_CURSO", await env.RecordStatusAsync(id));
    }

    // ── Soportes ───────────────────────────────────────────────────────────

    private static async Task<long> ScheduleOkAsync(MaintenanceEnv env, long assetId)
    {
        var response = await env.ScheduleAsync(assetId);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await MaintenanceEnv.ReadAsync(response)).GetProperty("maintenanceRecordId").GetInt64();
    }

    private static async Task<(long Id, int Version)> StartDirectOkAsync(MaintenanceEnv env, long assetId)
    {
        var response = await env.StartDirectAsync(assetId);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await MaintenanceEnv.ReadAsync(response);
        return (body.GetProperty("maintenanceRecordId").GetInt64(), body.GetProperty("version").GetInt32());
    }
}
