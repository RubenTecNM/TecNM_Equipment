using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Maintenance.GetMaintenance;
using Xunit.Abstractions;

namespace TecnmInventario.Api.Tests.Integration;

// ═══════════════════════════════════════════════════════════════════════════
// M.4: consultas de solo lectura del módulo de mantenimiento (listado, detalle,
// calendario y técnicos), por HTTP contra PostgreSQL real. Datos ficticios.
// ═══════════════════════════════════════════════════════════════════════════

internal sealed class FixedTimeProvider : TimeProvider
{
    private readonly DateTimeOffset _now;
    public FixedTimeProvider(DateTimeOffset now) => _now = now;
    public override DateTimeOffset GetUtcNow() => _now;
}

[Trait("Category", "Integration")]
public sealed class MaintenanceQueryTests : MaintenanceTestBase
{
    private static readonly DateTimeOffset Now = new(2030, 1, 15, 12, 0, 0, TimeSpan.Zero);
    private readonly ITestOutputHelper _output;

    public MaintenanceQueryTests(PostgresContainerFixture fixture, ITestOutputHelper output)
        : base(fixture) => _output = output;

    private Task<MaintenanceEnv> CreateEnvWithClockAsync()
        => CreateEnvAsync(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));
        });

    // ── A1: listado ────────────────────────────────────────────────────────

    [DockerFact]
    public async Task Listado_OrdenaPorFechaDeReferencia_YPagina_ConTotalCorrecto()
    {
        await using var env = await CreateEnvAsync();
        var asset = await env.InsertAssetAsync();
        // Fecha de referencia = COALESCE(performed_at, started_at, scheduled_at).
        var r2 = await env.InsertRecordAsync(asset, "COMPLETADO", scheduledAt: "2020-01-01T00:00:00Z",
            startedAt: "2030-02-01T00:00:00Z", performedAt: "2030-02-02T00:00:00Z");
        var r3 = await env.InsertRecordAsync(asset, "EN_CURSO", scheduledAt: "2025-01-01T00:00:00Z",
            startedAt: "2030-05-01T00:00:00Z");
        var r4 = await env.InsertRecordAsync(asset, "PROGRAMADO", scheduledAt: "2030-05-01T00:00:00Z"); // empata con r3: id DESC
        var r1 = await env.InsertRecordAsync(asset, "PROGRAMADO", scheduledAt: "2031-03-01T00:00:00Z");
        var r5 = await env.InsertRecordAsync(asset, "PROGRAMADO", scheduledAt: "2019-03-01T00:00:00Z");

        var all = await env.GetAsync("/api/maintenance?pageSize=50");
        Assert.Equal(HttpStatusCode.OK, all.Status);
        Assert.Equal(5, all.Body.GetProperty("totalCount").GetInt32());
        Assert.Equal(new[] { r1, r4, r3, r2, r5 }, MaintenanceEnv.Ids(all.Body.GetProperty("items")));

        var page1 = await env.GetAsync("/api/maintenance?pageNumber=1&pageSize=2");
        var page3 = await env.GetAsync("/api/maintenance?pageNumber=3&pageSize=2");
        Assert.Equal(new[] { r1, r4 }, MaintenanceEnv.Ids(page1.Body.GetProperty("items")));
        Assert.Equal(new[] { r5 }, MaintenanceEnv.Ids(page3.Body.GetProperty("items")));
        Assert.Equal(5, page1.Body.GetProperty("totalCount").GetInt32());
        Assert.Equal(3, page1.Body.GetProperty("totalPages").GetInt32());
    }

    [DockerFact]
    public async Task Listado_CadaFiltroPorSeparado_YCombinados_ConTotalCountFiltrado()
    {
        await using var env = await CreateEnvAsync();
        var assetA = await env.InsertAssetAsync();
        var assetB = await env.InsertAssetAsync();
        var (_, tecnico) = await env.CreateUserAsync("m4_tecnico", "TECNICO");

        var a1 = await env.InsertRecordAsync(assetA, "PROGRAMADO", scheduledAt: "2030-03-01T00:00:00Z",
            typeCode: "PREVENTIVO", description: "revisión general");
        var a2 = await env.InsertRecordAsync(assetA, "EN_CURSO", startedAt: "2030-01-10T00:00:00Z",
            typeCode: "CORRECTIVO", performedBy: tecnico, description: "cambio de fuente");
        var a3 = await env.InsertRecordAsync(assetA, "COMPLETADO", startedAt: "2029-05-01T00:00:00Z",
            performedAt: "2029-05-02T00:00:00Z", typeCode: "PREVENTIVO", performedBy: tecnico);
        var b1 = await env.InsertRecordAsync(assetB, "CANCELADO", scheduledAt: "2029-08-01T00:00:00Z",
            cancelledAt: "2029-08-02T00:00:00Z", typeCode: "CORRECTIVO", reason: "no procede");
        var b2 = await env.InsertRecordAsync(assetB, "PROGRAMADO", scheduledAt: "2030-06-01T00:00:00Z",
            typeCode: "PREVENTIVO");
        var corrective = await env.TypeIdAsync("CORRECTIVO");

        async Task<(long[] Ids, int Total)> Q(string query)
        {
            var r = await env.GetAsync("/api/maintenance?pageSize=1&" + query);
            Assert.Equal(HttpStatusCode.OK, r.Status);
            var ids = (await env.GetAsync("/api/maintenance?pageSize=50&" + query)).Body.GetProperty("items");
            return (MaintenanceEnv.Ids(ids).OrderBy(x => x).ToArray(), r.Body.GetProperty("totalCount").GetInt32());
        }

        var (ids, total) = await Q($"assetId={assetA}");
        Assert.Equal(new[] { a1, a2, a3 }, ids); Assert.Equal(3, total);

        (ids, total) = await Q("status=PROGRAMADO");
        Assert.Equal(new[] { a1, b2 }, ids); Assert.Equal(2, total);

        (ids, total) = await Q("status=PROGRAMADO&status=EN_CURSO");
        Assert.Equal(new[] { a1, a2, b2 }, ids); Assert.Equal(3, total);

        (ids, total) = await Q("onlyOpen=true");
        Assert.Equal(new[] { a1, a2, b2 }, ids); Assert.Equal(3, total);

        (ids, total) = await Q($"maintenanceTypeId={corrective}");
        Assert.Equal(new[] { a2, b1 }, ids); Assert.Equal(2, total);

        (ids, total) = await Q($"performedBy={tecnico}");
        Assert.Equal(new[] { a2, a3 }, ids); Assert.Equal(2, total);

        // from/to sobre la fecha de referencia (inclusivos).
        (ids, total) = await Q("from=2030-01-10T00:00:00Z&to=2030-03-01T00:00:00Z");
        Assert.Equal(new[] { a1, a2 }, ids); Assert.Equal(2, total);

        (ids, total) = await Q("to=2029-12-31T00:00:00Z");
        Assert.Equal(new[] { a3, b1 }, ids); Assert.Equal(2, total);

        (ids, total) = await Q("search=fuente");
        Assert.Equal(new[] { a2 }, ids); Assert.Equal(1, total);

        var code = await env.Db.ScalarAsync<string>($"SELECT inventory_code FROM inventario.assets WHERE asset_id = {assetB}");
        (ids, total) = await Q($"search={code.ToLowerInvariant()}"); // por código de inventario, sin distinguir mayúsculas
        Assert.Equal(new[] { b1, b2 }, ids); Assert.Equal(2, total);

        // Combinados.
        (ids, total) = await Q($"assetId={assetA}&status=PROGRAMADO&status=EN_CURSO&maintenanceTypeId={corrective}");
        Assert.Equal(new[] { a2 }, ids); Assert.Equal(1, total);

        (ids, total) = await Q($"onlyOpen=true&performedBy={tecnico}&from=2030-01-01T00:00:00Z");
        Assert.Equal(new[] { a2 }, ids); Assert.Equal(1, total);
    }

    [DockerTheory]
    [InlineData("status=NOEXISTE")]
    [InlineData("status=PROGRAMADO&status=XX")]
    public async Task Listado_ConEstadoDesconocido_Rechaza400(string query)
    {
        await using var env = await CreateEnvAsync();

        var response = await env.GetAsync("/api/maintenance?" + query);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
    }

    // ── A2: search escapa comodines ────────────────────────────────────────

    [DockerTheory]
    [InlineData("%", "50% de avance", "sin comodin")]
    [InlineData("_", "pieza_A", "piezaXA")]
    public async Task Listado_Search_EscapaLosComodinesDeLike(string term, string literal, string other)
    {
        await using var env = await CreateEnvAsync();
        var asset = await env.InsertAssetAsync();
        var hit = await env.InsertRecordAsync(asset, "PROGRAMADO", scheduledAt: "2030-03-01T00:00:00Z", description: literal);
        await env.InsertRecordAsync(asset, "PROGRAMADO", scheduledAt: "2030-03-02T00:00:00Z", description: other);
        await env.InsertRecordAsync(asset, "PROGRAMADO", scheduledAt: "2030-03-03T00:00:00Z", description: null);

        var response = await env.GetAsync($"/api/maintenance?search={Uri.EscapeDataString(term)}");

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Equal(new[] { hit }, MaintenanceEnv.Ids(response.Body.GetProperty("items")));
        Assert.Equal(1, response.Body.GetProperty("totalCount").GetInt32());
    }

    // ── A3: activo borrado ─────────────────────────────────────────────────

    [DockerFact]
    public async Task Listado_ActivoBorrado_SuRegistroAparece_ConAssetIsDeleted()
    {
        await using var env = await CreateEnvAsync();
        var live = await env.InsertAssetAsync();
        var dead = await env.InsertAssetAsync();
        var liveRecord = await env.InsertRecordAsync(live, "PROGRAMADO", scheduledAt: "2030-03-01T00:00:00Z");
        var deadRecord = await env.InsertRecordAsync(dead, "PROGRAMADO", scheduledAt: "2030-03-02T00:00:00Z");
        await env.SetAssetAsync(dead, "is_deleted = true, deleted_at = now()");

        var all = await env.GetAsync("/api/maintenance");
        var byAsset = await env.GetAsync($"/api/maintenance?assetId={dead}");
        var missing = await env.GetAsync("/api/maintenance?assetId=987654");

        Assert.Equal(HttpStatusCode.OK, all.Status);
        Assert.Equal(new[] { deadRecord, liveRecord }, MaintenanceEnv.Ids(all.Body.GetProperty("items")));
        var first = all.Body.GetProperty("items")[0];
        Assert.True(first.GetProperty("assetIsDeleted").GetBoolean());
        Assert.False(all.Body.GetProperty("items")[1].GetProperty("assetIsDeleted").GetBoolean());

        Assert.Equal(HttpStatusCode.OK, byAsset.Status);
        Assert.Equal(new[] { deadRecord }, MaintenanceEnv.Ids(byAsset.Body.GetProperty("items")));
        Assert.Equal(HttpStatusCode.NotFound, missing.Status);

        var detail = await env.GetAsync($"/api/maintenance/{deadRecord}");
        Assert.Equal(HttpStatusCode.OK, detail.Status);
        Assert.True(detail.Body.GetProperty("assetIsDeleted").GetBoolean());
    }

    // ── A4: registro borrado ───────────────────────────────────────────────

    [DockerFact]
    public async Task RegistroBorrado_NoApareceEnListadoNiCalendario_YElDetalleEs404()
    {
        await using var env = await CreateEnvWithClockAsync();
        var asset = await env.InsertAssetAsync();
        var visible = await env.InsertRecordAsync(asset, "PROGRAMADO", scheduledAt: "2030-01-20T00:00:00Z");
        var deleted = await env.InsertRecordAsync(asset, "PROGRAMADO", scheduledAt: "2030-01-21T00:00:00Z", deleted: true);
        var deletedOverdue = await env.InsertRecordAsync(asset, "PROGRAMADO", scheduledAt: "2029-01-01T00:00:00Z", deleted: true);

        var list = await env.GetAsync("/api/maintenance");
        var schedule = await env.GetAsync("/api/maintenance/schedule");
        var detail = await env.GetAsync($"/api/maintenance/{deleted}");

        Assert.Equal(new[] { visible }, MaintenanceEnv.Ids(list.Body.GetProperty("items")));
        Assert.Equal(1, list.Body.GetProperty("totalCount").GetInt32());
        Assert.Equal(new[] { visible }, MaintenanceEnv.Ids(schedule.Body.GetProperty("upcoming")));
        Assert.Empty(MaintenanceEnv.Ids(schedule.Body.GetProperty("overdue")));
        Assert.Equal(HttpStatusCode.NotFound, detail.Status);
        Assert.Equal(HttpStatusCode.NotFound, (await env.GetAsync($"/api/maintenance/{deletedOverdue}")).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await env.GetAsync("/api/maintenance/987654")).Status);
    }

    // ── A5: detalle completo ───────────────────────────────────────────────

    [DockerFact]
    public async Task Detalle_DevuelveEstadosIncidenteCostoYCancelacion_SinCorreos()
    {
        await using var env = await CreateEnvAsync();
        var asset = await env.InsertAssetAsync();
        await env.SetAssetAsync(asset, "hostname = 'pc-lab-01'");
        var incident = await env.InsertIncidentAsync(asset, "EN_REPARACION");
        var (_, tecnico) = await env.CreateUserAsync("m4_tec_detalle", "TECNICO");

        var done = await env.InsertRecordAsync(asset, "COMPLETADO", scheduledAt: "2030-01-01T00:00:00Z",
            startedAt: "2030-01-02T00:00:00Z", performedAt: "2030-01-03T00:00:00Z", nextAt: "2030-07-01T00:00:00Z",
            typeCode: "CORRECTIVO", description: "Cambio de fuente", partsUsed: "Fuente 500W", cost: 1234.50m,
            incidentId: incident, performedBy: tecnico, statusBefore: "CON_FALLA", statusAfter: "DISPONIBLE");
        var cancelled = await env.InsertRecordAsync(asset, "CANCELADO", scheduledAt: "2030-02-01T00:00:00Z",
            cancelledAt: "2030-02-02T00:00:00Z", reason: "Sin refacciones");

        var detail = await env.GetAsync($"/api/maintenance/{done}");

        Assert.Equal(HttpStatusCode.OK, detail.Status);
        var b = detail.Body;
        Assert.Equal(done, b.GetProperty("maintenanceRecordId").GetInt64());
        Assert.Equal("COMPLETADO", b.GetProperty("status").GetString());
        Assert.Equal("pc-lab-01", b.GetProperty("assetHostname").GetString());
        Assert.Equal("CORRECTIVO", b.GetProperty("maintenanceTypeCode").GetString());
        Assert.Equal("Cambio de fuente", b.GetProperty("description").GetString());
        Assert.Equal("Fuente 500W", b.GetProperty("partsUsed").GetString());
        Assert.Equal(1234.50m, b.GetProperty("cost").GetDecimal());
        Assert.Equal("CON_FALLA", b.GetProperty("statusBefore").GetProperty("code").GetString());
        Assert.False(string.IsNullOrEmpty(b.GetProperty("statusBefore").GetProperty("name").GetString()));
        Assert.Equal("DISPONIBLE", b.GetProperty("statusAfter").GetProperty("code").GetString());
        Assert.Equal(incident, b.GetProperty("incident").GetProperty("id").GetInt64());
        Assert.Equal("EN_REPARACION", b.GetProperty("incident").GetProperty("status").GetString());
        Assert.Equal("Falla ficticia M.2", b.GetProperty("incident").GetProperty("title").GetString());
        Assert.Equal(tecnico, b.GetProperty("performedBy").GetProperty("id").GetInt64());
        Assert.Equal("Usuario Mantenimiento", b.GetProperty("performedBy").GetProperty("fullName").GetString());
        Assert.Equal(env.AdminId, b.GetProperty("createdBy").GetProperty("id").GetInt64());
        Assert.True(b.TryGetProperty("createdAt", out _));
        Assert.DoesNotContain("@", detail.Raw);

        var c = (await env.GetAsync($"/api/maintenance/{cancelled}")).Body;
        Assert.Equal("Sin refacciones", c.GetProperty("cancellationReason").GetString());
        Assert.NotEqual(System.Text.Json.JsonValueKind.Null, c.GetProperty("cancelledAt").ValueKind);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, c.GetProperty("incident").ValueKind);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, c.GetProperty("statusBefore").ValueKind);

        // El listado tampoco expone correos.
        Assert.DoesNotContain("@", (await env.GetAsync("/api/maintenance")).Raw);
    }

    // ── A6: permisos ───────────────────────────────────────────────────────

    [DockerFact]
    public async Task Permisos_ResponsableAreaVe_PeroNoListaTecnicos_UsuarioGeneralNoVeNada()
    {
        await using var env = await CreateEnvAsync();
        var asset = await env.InsertAssetAsync();
        var record = await env.InsertRecordAsync(asset, "PROGRAMADO", scheduledAt: "2030-03-01T00:00:00Z");
        var (responsable, _) = await env.CreateUserAsync("m4_resp", "RESPONSABLE_AREA");
        var (general, _) = await env.CreateUserAsync("m4_general", "USUARIO_GENERAL");

        Assert.Equal(HttpStatusCode.OK, (await env.GetAsync("/api/maintenance", responsable)).Status);
        Assert.Equal(HttpStatusCode.OK, (await env.GetAsync($"/api/maintenance/{record}", responsable)).Status);
        Assert.Equal(HttpStatusCode.OK, (await env.GetAsync("/api/maintenance/schedule", responsable)).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await env.GetAsync("/api/maintenance/technicians", responsable)).Status);

        foreach (var path in new[] { "/api/maintenance", $"/api/maintenance/{record}", "/api/maintenance/schedule", "/api/maintenance/technicians" })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await env.GetAsync(path, general)).Status);
        }

        var anonymous = await env.Client.GetAsync("/api/maintenance");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    // ── A7: calendario con reloj fijo ──────────────────────────────────────

    [DockerFact]
    public async Task Calendario_Vencidos_ProximosYBordeExactoDelDiaN()
    {
        await using var env = await CreateEnvWithClockAsync();
        var asset = await env.InsertAssetAsync();
        var overdue1 = await env.InsertRecordAsync(asset, "PROGRAMADO", scheduledAt: "2030-01-15T11:59:59Z"); // 1 s antes de ahora
        var overdue2 = await env.InsertRecordAsync(asset, "PROGRAMADO", scheduledAt: "2029-12-01T00:00:00Z");
        var atNow = await env.InsertRecordAsync(asset, "PROGRAMADO", scheduledAt: "2030-01-15T12:00:00Z");  // ahora exacto: próximo
        var day1 = await env.InsertRecordAsync(asset, "PROGRAMADO", scheduledAt: "2030-01-16T12:00:00Z");
        var edge = await env.InsertRecordAsync(asset, "PROGRAMADO", scheduledAt: "2030-02-14T12:00:00Z");   // ahora + 30 d exactos
        var beyond = await env.InsertRecordAsync(asset, "PROGRAMADO", scheduledAt: "2030-02-14T12:00:01Z"); // +1 s: fuera
        // No cuentan: EN_CURSO y COMPLETADO con fecha dentro de la ventana.
        await env.InsertRecordAsync(await env.InsertAssetAsync(), "EN_CURSO", scheduledAt: "2030-01-20T00:00:00Z", startedAt: "2030-01-15T00:00:00Z");
        await env.InsertRecordAsync(await env.InsertAssetAsync(), "COMPLETADO", scheduledAt: "2030-01-20T00:00:00Z",
            startedAt: "2030-01-10T00:00:00Z", performedAt: "2030-01-11T00:00:00Z");
        // Activo dado de baja y activo borrado: excluidos.
        var disposed = await env.InsertAssetAsync("DADO_DE_BAJA");
        await env.InsertRecordAsync(disposed, "PROGRAMADO", scheduledAt: "2030-01-20T00:00:00Z");
        await env.InsertRecordAsync(disposed, "PROGRAMADO", scheduledAt: "2029-01-01T00:00:00Z");
        var deletedAsset = await env.InsertAssetAsync();
        await env.InsertRecordAsync(deletedAsset, "PROGRAMADO", scheduledAt: "2030-01-20T00:00:00Z");
        await env.SetAssetAsync(deletedAsset, "is_deleted = true, deleted_at = now()");

        var response = await env.GetAsync("/api/maintenance/schedule");

        Assert.Equal(HttpStatusCode.OK, response.Status);
        var b = response.Body;
        Assert.Equal(30, b.GetProperty("windowDays").GetInt32());
        Assert.Equal(Now.UtcDateTime, b.GetProperty("generatedAt").GetDateTime().ToUniversalTime());
        Assert.Equal(new[] { overdue2, overdue1 }, MaintenanceEnv.Ids(b.GetProperty("overdue")));
        Assert.Equal(new[] { atNow, day1, edge }, MaintenanceEnv.Ids(b.GetProperty("upcoming")));
        Assert.DoesNotContain(beyond, MaintenanceEnv.Ids(b.GetProperty("upcoming")));
        Assert.Equal(2, b.GetProperty("counts").GetProperty("overdue").GetInt32());
        Assert.Equal(3, b.GetProperty("counts").GetProperty("upcoming").GetInt32());
        Assert.Equal(0, b.GetProperty("counts").GetProperty("dueWithoutSchedule").GetInt32());

        // days=7: el borde cambia.
        var week = (await env.GetAsync("/api/maintenance/schedule?days=7")).Body;
        Assert.Equal(7, week.GetProperty("windowDays").GetInt32());
        Assert.Equal(new[] { atNow, day1 }, MaintenanceEnv.Ids(week.GetProperty("upcoming")));
    }

    [DockerFact]
    public async Task Calendario_DueWithoutSchedule_SoloElUltimoPreventivo_SinProgramadosNiEnCurso()
    {
        await using var env = await CreateEnvWithClockAsync();

        // Activo A: último preventivo con próximo mantenimiento dentro de la ventana -> aparece.
        var a = await env.InsertAssetAsync();
        var aRecord = await env.InsertRecordAsync(a, "COMPLETADO", startedAt: "2029-10-01T00:00:00Z",
            performedAt: "2029-10-02T00:00:00Z", nextAt: "2030-02-01T00:00:00Z");
        // Borde exacto del día N (ahora + 30 d) -> aparece; +1 s -> no.
        var edge = await env.InsertAssetAsync();
        var edgeRecord = await env.InsertRecordAsync(edge, "COMPLETADO", startedAt: "2029-10-01T00:00:00Z",
            performedAt: "2029-10-02T00:00:00Z", nextAt: "2030-02-14T12:00:00Z");
        var beyond = await env.InsertAssetAsync();
        await env.InsertRecordAsync(beyond, "COMPLETADO", startedAt: "2029-10-01T00:00:00Z",
            performedAt: "2029-10-02T00:00:00Z", nextAt: "2030-02-14T12:00:01Z");
        // Dos preventivos: solo cuenta el ÚLTIMO (el nuevo ya no vence) -> no aparece.
        var twice = await env.InsertAssetAsync();
        await env.InsertRecordAsync(twice, "COMPLETADO", startedAt: "2029-01-01T00:00:00Z",
            performedAt: "2029-01-02T00:00:00Z", nextAt: "2030-01-01T00:00:00Z");
        await env.InsertRecordAsync(twice, "COMPLETADO", startedAt: "2029-12-01T00:00:00Z",
            performedAt: "2029-12-02T00:00:00Z", nextAt: "2031-01-01T00:00:00Z");
        // Con un PROGRAMADO o un EN_CURSO (de cualquier tipo) del mismo activo -> no aparece.
        var withScheduled = await env.InsertAssetAsync();
        await env.InsertRecordAsync(withScheduled, "COMPLETADO", startedAt: "2029-10-01T00:00:00Z",
            performedAt: "2029-10-02T00:00:00Z", nextAt: "2030-02-01T00:00:00Z");
        await env.InsertRecordAsync(withScheduled, "PROGRAMADO", scheduledAt: "2030-06-01T00:00:00Z", typeCode: "CORRECTIVO");
        var withInProgress = await env.InsertAssetAsync();
        await env.InsertRecordAsync(withInProgress, "COMPLETADO", startedAt: "2029-10-01T00:00:00Z",
            performedAt: "2029-10-02T00:00:00Z", nextAt: "2030-02-01T00:00:00Z");
        await env.InsertRecordAsync(withInProgress, "EN_CURSO", startedAt: "2030-01-14T00:00:00Z");
        // El correctivo no cuenta como preventivo.
        var corrective = await env.InsertAssetAsync();
        await env.InsertRecordAsync(corrective, "COMPLETADO", startedAt: "2029-10-01T00:00:00Z",
            performedAt: "2029-10-02T00:00:00Z", nextAt: "2030-02-01T00:00:00Z", typeCode: "CORRECTIVO");
        // Dado de baja y borrado: excluidos.
        var disposed = await env.InsertAssetAsync("DADO_DE_BAJA");
        await env.InsertRecordAsync(disposed, "COMPLETADO", startedAt: "2029-10-01T00:00:00Z",
            performedAt: "2029-10-02T00:00:00Z", nextAt: "2030-02-01T00:00:00Z");
        var deletedAsset = await env.InsertAssetAsync();
        await env.InsertRecordAsync(deletedAsset, "COMPLETADO", startedAt: "2029-10-01T00:00:00Z",
            performedAt: "2029-10-02T00:00:00Z", nextAt: "2030-02-01T00:00:00Z");
        await env.SetAssetAsync(deletedAsset, "is_deleted = true, deleted_at = now()");
        // Registro borrado: no cuenta.
        var deletedRecordAsset = await env.InsertAssetAsync();
        await env.InsertRecordAsync(deletedRecordAsset, "COMPLETADO", startedAt: "2029-10-01T00:00:00Z",
            performedAt: "2029-10-02T00:00:00Z", nextAt: "2030-02-01T00:00:00Z", deleted: true);

        var due = (await env.GetAsync("/api/maintenance/schedule")).Body;

        Assert.Equal(new[] { aRecord, edgeRecord }, MaintenanceEnv.Ids(due.GetProperty("dueWithoutSchedule")));
        Assert.Equal(2, due.GetProperty("counts").GetProperty("dueWithoutSchedule").GetInt32());
        var item = due.GetProperty("dueWithoutSchedule")[0];
        Assert.Equal(a, item.GetProperty("assetId").GetInt64());
        Assert.Equal(new DateTime(2030, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            item.GetProperty("nextMaintenanceAt").GetDateTime().ToUniversalTime());
    }

    [DockerFact]
    public async Task Calendario_DueWithoutSchedule_DesapareceAlProgramarOtro()
    {
        await using var env = await CreateEnvWithClockAsync();
        var asset = await env.InsertAssetAsync();
        var last = await env.InsertRecordAsync(asset, "COMPLETADO", startedAt: "2029-10-01T00:00:00Z",
            performedAt: "2029-10-02T00:00:00Z", nextAt: "2030-02-01T00:00:00Z");

        var before = (await env.GetAsync("/api/maintenance/schedule")).Body;
        Assert.Equal(new[] { last }, MaintenanceEnv.Ids(before.GetProperty("dueWithoutSchedule")));

        var scheduled = await env.PostAsync("/api/maintenance", new
        {
            assetId = asset,
            maintenanceTypeId = env.TypeId,
            scheduledAt = "2030-03-01T09:00:00Z"
        });
        Assert.Equal(HttpStatusCode.Created, scheduled.StatusCode);

        var after = (await env.GetAsync("/api/maintenance/schedule")).Body;
        Assert.Empty(MaintenanceEnv.Ids(after.GetProperty("dueWithoutSchedule")));
        Assert.Equal(0, after.GetProperty("counts").GetProperty("dueWithoutSchedule").GetInt32());
    }

    [DockerTheory]
    [InlineData("days=0")]
    [InlineData("days=366")]
    [InlineData("days=-5")]
    [InlineData("days=abc")]
    public async Task Calendario_ConDiasFueraDeRango_Rechaza400(string query)
    {
        await using var env = await CreateEnvWithClockAsync();

        var response = await env.GetAsync("/api/maintenance/schedule?" + query);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
    }

    [DockerTheory]
    [InlineData("days=1", 1)]
    [InlineData("days=365", 365)]
    [InlineData("", 30)]
    public async Task Calendario_DiasValidosYValorPorDefecto(string query, int expected)
    {
        await using var env = await CreateEnvWithClockAsync();

        var response = await env.GetAsync("/api/maintenance/schedule?" + query);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Equal(expected, response.Body.GetProperty("windowDays").GetInt32());
    }

    [DockerFact]
    public async Task Calendario_SinRelojInyectado_UsaElRelojDelSistema()
    {
        await using var env = await CreateEnvAsync(); // sin reloj fijo: TimeProvider real del host
        var response = await env.GetAsync("/api/maintenance/schedule");
        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Equal(30, response.Body.GetProperty("windowDays").GetInt32());
        Assert.True(Math.Abs((response.Body.GetProperty("generatedAt").GetDateTime().ToUniversalTime() - DateTime.UtcNow).TotalMinutes) < 5);
    }

    // ── A8: técnicos ───────────────────────────────────────────────────────

    [DockerFact]
    public async Task Tecnicos_IncluyeSoloActivosNoBorradosConRolConPermiso_Ordenados()
    {
        await using var env = await CreateEnvAsync();
        var (_, zeta) = await env.CreateUserAsync("m4_zeta", "TECNICO");
        var (_, alfa) = await env.CreateUserAsync("m4_alfa", "TECNICO");
        await env.Db.ExecuteAsync($"UPDATE inventario.app_users SET first_name = 'Zeta' WHERE user_id = {zeta}");
        await env.Db.ExecuteAsync($"UPDATE inventario.app_users SET first_name = 'Alfa' WHERE user_id = {alfa}");
        var (_, inactive) = await env.CreateUserAsync("m4_inactivo", "TECNICO");
        await env.Db.ExecuteAsync($"UPDATE inventario.app_users SET status = 'inactivo' WHERE user_id = {inactive}");
        var (_, removed) = await env.CreateUserAsync("m4_borrado", "TECNICO");
        await env.Db.ExecuteAsync($"UPDATE inventario.app_users SET is_deleted = true, deleted_at = now() WHERE user_id = {removed}");
        var (_, responsable) = await env.CreateUserAsync("m4_resp_tec", "RESPONSABLE_AREA");
        var (_, general) = await env.CreateUserAsync("m4_gen_tec", "USUARIO_GENERAL");

        // Usuario de un rol con maintenance.manage pero borrado lógicamente.
        var deletedRoleUser = await env.Db.ScalarAsync<long>("""
            WITH r AS (
                INSERT INTO inventario.roles (code, name, is_deleted, deleted_at)
                VALUES ('ROL_M4_BORRADO', 'Rol borrado de prueba', true, now()) RETURNING role_id),
            rp AS (
                INSERT INTO inventario.role_permissions (role_id, permission_id)
                SELECT r.role_id, p.permission_id FROM r, inventario.permissions p WHERE p.code = 'maintenance.manage')
            INSERT INTO inventario.app_users (username, email, password_hash, first_name, last_name, role_id, status)
            SELECT 'm4_rol_borrado', 'm4_rol_borrado@test.local', 'x', 'Rol', 'Borrado', r.role_id, 'activo' FROM r
            RETURNING user_id
            """);

        var response = await env.GetAsync("/api/maintenance/technicians");

        Assert.Equal(HttpStatusCode.OK, response.Status);
        var ids = response.Body.EnumerateArray().Select(e => e.GetProperty("id").GetInt64()).ToArray();
        Assert.Contains(alfa, ids);
        Assert.Contains(zeta, ids);
        Assert.Contains(env.AdminId, ids);
        Assert.DoesNotContain(inactive, ids);
        Assert.DoesNotContain(removed, ids);
        Assert.DoesNotContain(responsable, ids);
        Assert.DoesNotContain(general, ids);
        Assert.DoesNotContain(deletedRoleUser, ids);
        Assert.True(Array.IndexOf(ids, alfa) < Array.IndexOf(ids, zeta)); // por nombre
        Assert.DoesNotContain("@", response.Raw);
        Assert.Contains("fullName", response.Raw);
    }

    // ── A9: rutas ──────────────────────────────────────────────────────────

    [DockerFact]
    public async Task Rutas_ScheduleYTechnicians_NoCaenEnElDetallePorId()
    {
        await using var env = await CreateEnvWithClockAsync();

        var schedule = await env.GetAsync("/api/maintenance/schedule");
        var technicians = await env.GetAsync("/api/maintenance/technicians");

        Assert.Equal(HttpStatusCode.OK, schedule.Status);
        Assert.True(schedule.Body.TryGetProperty("overdue", out _)); // el calendario, no un 404 del detalle
        Assert.Equal(HttpStatusCode.OK, technicians.Status);
        Assert.StartsWith("[", technicians.Raw.TrimStart());
        Assert.Equal(HttpStatusCode.NotFound, (await env.GetAsync("/api/maintenance/abc")).Status);
    }

    // ── SQL del listado: filtra y pagina en BD (se revisa con ToQueryString) ─

    [DockerFact]
    public async Task Listado_ElSqlFiltraYPaginaEnLaBaseDeDatos()
    {
        await using var db = await CreateDatabaseAsync();
        var options = new DbContextOptionsBuilder<TecnmInventarioDbContext>().UseNpgsql(db.ConnectionString).Options;
        await using var context = new TecnmInventarioDbContext(options);

        var query = new GetMaintenanceQuery(2, 10, null, new[] { "PROGRAMADO", "EN_CURSO" }, null, null, null, null, "revisión", false);
        var sql = GetMaintenanceHandler.BuildFilteredQuery(context, query).ToQueryString();
        var page = GetMaintenanceHandler.ApplyPaging(
            GetMaintenanceHandler.BuildFilteredQuery(context, query), query).ToQueryString();
        _output.WriteLine("SQL FILTRADO:\n" + sql);
        _output.WriteLine("SQL PAGINADO:\n" + page);
        _output.WriteLine("SQL DE ITEMS (con proyección):\n" + GetMaintenanceHandler.ApplyPaging(
            GetMaintenanceHandler.BuildFilteredQuery(context, query), query)
            .Select(TecnmInventario.Api.Features.Maintenance.Shared.MaintenanceProjections.ListItem).ToQueryString());
        _output.WriteLine("SQL DE TOTALCOUNT:\n" + GetMaintenanceHandler.BuildFilteredQuery(context, query)
            .Select(m => m.MaintenanceRecordId).ToQueryString());

        Assert.Contains("WHERE", sql);
        Assert.Contains("status", sql);
        Assert.Contains("ILIKE", sql);
        Assert.Contains("LIMIT", page);
        Assert.Contains("OFFSET", page);
        Assert.Contains("ORDER BY", page);
    }
}
