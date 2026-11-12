using System.Net;
using System.Text;

namespace TecnmInventario.Api.Tests.Integration;

// ═══════════════════════════════════════════════════════════════════════════
// M.5: editar y borrar mantenimientos + evidencias (OwnerType MAINTENANCE) de
// extremo a extremo. HTTP contra PostgreSQL real. Datos ficticios.
// ═══════════════════════════════════════════════════════════════════════════

[Trait("Category", "Integration")]
public sealed class MaintenanceEditTests : MaintenanceTestBase
{
    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.4\n% evidencia ficticia M.5\n");
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D];

    public MaintenanceEditTests(PostgresContainerFixture fixture)
        : base(fixture) { }

    // ── A1: Update PROGRAMADO ──────────────────────────────────────────────

    [DockerFact]
    public async Task Update_Programado_CambiaTodoLoEditable_SubeVersionYAudita()
    {
        await using var env = await CreateEnvAsync();
        var asset = await env.InsertAssetAsync();
        var (_, tecnico) = await env.CreateUserAsync("m5_tecnico", "TECNICO");
        var corrective = await env.TypeIdAsync("CORRECTIVO");
        var id = await env.InsertRecordAsync(asset, "PROGRAMADO", scheduledAt: "2030-03-01T09:00:00Z", description: "original");
        var assetVersion = await env.AssetVersionAsync(asset);

        var response = await env.UpdateAsync(id, new
        {
            expectedVersion = 1,
            scheduledAt = "2031-05-05T10:00:00Z",
            maintenanceTypeId = corrective,
            performedBy = tecnico,
            description = "nueva descripción"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await MaintenanceEnv.ReadAsync(response);
        Assert.Equal(id, body.GetProperty("maintenanceRecordId").GetInt64());
        Assert.Equal("PROGRAMADO", body.GetProperty("status").GetString());
        Assert.True(body.GetProperty("version").GetInt32() > 1);
        Assert.Equal(body.GetProperty("version").GetInt32(), await env.RecordVersionAsync(id));

        Assert.Equal("2031-05-05T10:00:00.000000Z", await env.IsoAsync(id, "scheduled_at"));
        Assert.Equal(corrective, await env.Db.ScalarAsync<long>(
            $"SELECT maintenance_type_id FROM inventario.maintenance_records WHERE maintenance_record_id = {id}"));
        Assert.Equal(tecnico, await env.Db.ScalarAsync<long>(
            $"SELECT performed_by FROM inventario.maintenance_records WHERE maintenance_record_id = {id}"));
        Assert.Equal("nueva descripción", await env.ScalarOrNullAsync(
            $"SELECT description FROM inventario.maintenance_records WHERE maintenance_record_id = {id}"));
        Assert.Equal(assetVersion, await env.AssetVersionAsync(asset)); // el activo no se toca

        Assert.Equal(1, await env.AuditCountAsync("maintenance_records", id, "UPDATE"));
        Assert.Equal("original", await env.ScalarOrNullAsync($"""
            SELECT old_data->>'Description' FROM inventario.audit_log
            WHERE table_name = 'maintenance_records' AND record_id = {id} AND action = 'UPDATE'
            """));
        Assert.Equal("nueva descripción", await env.ScalarOrNullAsync($"""
            SELECT new_data->>'Description' FROM inventario.audit_log
            WHERE table_name = 'maintenance_records' AND record_id = {id} AND action = 'UPDATE'
            """));
        Assert.Equal(corrective.ToString(), await env.ScalarOrNullAsync($"""
            SELECT new_data->>'MaintenanceTypeId' FROM inventario.audit_log
            WHERE table_name = 'maintenance_records' AND record_id = {id} AND action = 'UPDATE'
            """));
        Assert.Equal(env.TypeId.ToString(), await env.ScalarOrNullAsync($"""
            SELECT old_data->>'MaintenanceTypeId' FROM inventario.audit_log
            WHERE table_name = 'maintenance_records' AND record_id = {id} AND action = 'UPDATE'
            """));
    }

    // ── A2: Update EN_CURSO ────────────────────────────────────────────────

    [DockerFact]
    public async Task Update_EnCurso_TecnicoYDescripcionOk_CamposNoEditablesDistintos400_Iguales_SeIgnoran()
    {
        await using var env = await CreateEnvAsync();
        var asset = await env.InsertAssetAsync();
        var (_, tecnico) = await env.CreateUserAsync("m5_tec_curso", "TECNICO");
        var corrective = await env.TypeIdAsync("CORRECTIVO");
        var id = await env.InsertScheduledAsync(asset); // PROGRAMADO con scheduled_at = ahora + 7 d
        var started = await env.StartAsync(id, 1);
        Assert.Equal(HttpStatusCode.OK, started.StatusCode);
        var scheduledIso = await env.IsoAsync(id, "scheduled_at");

        // Distinto -> 400 que nombra el campo.
        var version = await env.RecordVersionAsync(id);
        var scheduleChange = await env.UpdateAsync(id, new { expectedVersion = version, scheduledAt = "2035-01-01T00:00:00Z" });
        Assert.Equal(HttpStatusCode.BadRequest, scheduleChange.StatusCode);
        Assert.Contains("scheduledAt", await scheduleChange.Content.ReadAsStringAsync());

        var typeChange = await env.UpdateAsync(id, new { expectedVersion = version, maintenanceTypeId = corrective });
        Assert.Equal(HttpStatusCode.BadRequest, typeChange.StatusCode);
        Assert.Contains("maintenanceTypeId", await typeChange.Content.ReadAsStringAsync());
        Assert.Equal(version, await env.RecordVersionAsync(id)); // nada cambió

        // Igual -> se ignora; técnico y descripción sí se editan.
        var ok = await env.UpdateAsync(id, new
        {
            expectedVersion = version,
            scheduledAt = scheduledIso,
            maintenanceTypeId = env.TypeId,
            performedBy = tecnico,
            description = "en curso, editada"
        });

        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal("EN_CURSO", await env.RecordStatusAsync(id));
        Assert.Equal(tecnico, await env.Db.ScalarAsync<long>(
            $"SELECT performed_by FROM inventario.maintenance_records WHERE maintenance_record_id = {id}"));
        Assert.Equal("en curso, editada", await env.ScalarOrNullAsync(
            $"SELECT description FROM inventario.maintenance_records WHERE maintenance_record_id = {id}"));
        Assert.Equal(scheduledIso, await env.IsoAsync(id, "scheduled_at"));
        Assert.Equal(env.TypeId, await env.Db.ScalarAsync<long>(
            $"SELECT maintenance_type_id FROM inventario.maintenance_records WHERE maintenance_record_id = {id}"));
    }

    // ── A3: terminales ─────────────────────────────────────────────────────

    [DockerTheory]
    [InlineData("COMPLETADO")]
    [InlineData("CANCELADO")]
    public async Task Update_CompletadoOCancelado_Rechaza400(string status)
    {
        await using var env = await CreateEnvAsync();
        var asset = await env.InsertAssetAsync();
        var id = status == "COMPLETADO"
            ? await env.InsertRecordAsync(asset, "COMPLETADO", startedAt: "2030-01-01T00:00:00Z", performedAt: "2030-01-02T00:00:00Z")
            : await env.InsertRecordAsync(asset, "CANCELADO", scheduledAt: "2030-01-01T00:00:00Z", cancelledAt: "2030-01-02T00:00:00Z", reason: "x");

        var response = await env.UpdateAsync(id, new { expectedVersion = 1, description = "intento" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Un mantenimiento completado o cancelado no se puede editar.", await response.Content.ReadAsStringAsync());
        Assert.Equal(1, await env.RecordVersionAsync(id));
        Assert.Equal(status, await env.RecordStatusAsync(id));
    }

    // ── A4: sin cambios reales ─────────────────────────────────────────────

    [DockerFact]
    public async Task Update_SinCambiosReales_200_SinSubirVersion_NiAuditar()
    {
        await using var env = await CreateEnvAsync();
        var asset = await env.InsertAssetAsync();
        var id = await env.InsertRecordAsync(asset, "PROGRAMADO", scheduledAt: "2030-03-01T09:00:00Z", description: "igual");
        var scheduledIso = await env.IsoAsync(id, "scheduled_at");
        var auditBefore = await env.Db.CountAsync("audit_log");

        var response = await env.UpdateAsync(id, new
        {
            expectedVersion = 1,
            scheduledAt = scheduledIso,
            maintenanceTypeId = env.TypeId,
            performedBy = (long?)null,
            description = "igual"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, (await MaintenanceEnv.ReadAsync(response)).GetProperty("version").GetInt32());
        Assert.Equal(1, await env.RecordVersionAsync(id));
        Assert.Equal(auditBefore, await env.Db.CountAsync("audit_log"));
    }

    // ── A5: versión vieja y validaciones ───────────────────────────────────

    [DockerFact]
    public async Task Update_ConVersionVieja_409_YNadaCambia()
    {
        await using var env = await CreateEnvAsync();
        var asset = await env.InsertAssetAsync();
        var id = await env.InsertRecordAsync(asset, "PROGRAMADO", scheduledAt: "2030-03-01T09:00:00Z", description: "original");
        await env.Db.ExecuteAsync($"UPDATE inventario.maintenance_records SET description = 'otro editó' WHERE maintenance_record_id = {id}");
        var auditBefore = await env.Db.CountAsync("audit_log");

        // Los datos son distintos y la versión (1) ya no es la vigente (2).
        var response = await env.UpdateAsync(id, new { expectedVersion = 1, description = "mi cambio" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("otro editó", await env.ScalarOrNullAsync(
            $"SELECT description FROM inventario.maintenance_records WHERE maintenance_record_id = {id}"));
        Assert.Equal(2, await env.RecordVersionAsync(id));
        Assert.Equal(auditBefore, await env.Db.CountAsync("audit_log"));
    }

    [DockerFact]
    public async Task Update_ConVersionVieja_PeroSinCambios_TambienEs409()
    {
        await using var env = await CreateEnvAsync();
        var asset = await env.InsertAssetAsync();
        var id = await env.InsertRecordAsync(asset, "PROGRAMADO", scheduledAt: "2030-03-01T09:00:00Z", description: "igual");
        await env.Db.ExecuteAsync($"UPDATE inventario.maintenance_records SET description = 'igual' || '' , cost = 1 WHERE maintenance_record_id = {id}");

        var response = await env.UpdateAsync(id, new { expectedVersion = 1, description = "igual" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [DockerTheory]
    [InlineData("tecnico_sin_permiso")]
    [InlineData("tecnico_inactivo")]
    [InlineData("tipo_borrado")]
    [InlineData("tipo_inexistente")]
    [InlineData("activo_borrado")]
    [InlineData("version_cero")]
    public async Task Update_ConDatosInvalidos_Rechaza400_SinCambios(string caso)
    {
        await using var env = await CreateEnvAsync();
        var asset = await env.InsertAssetAsync();
        var corrective = await env.TypeIdAsync("CORRECTIVO");
        var id = await env.InsertRecordAsync(asset, "PROGRAMADO", scheduledAt: "2030-03-01T09:00:00Z", description: "original");
        object body = new { expectedVersion = 1, description = "cambio" };

        switch (caso)
        {
            case "tecnico_sin_permiso":
                body = new { expectedVersion = 1, performedBy = (await env.CreateUserAsync("m5_resp", "RESPONSABLE_AREA")).UserId };
                break;
            case "tecnico_inactivo":
                var (_, inactive) = await env.CreateUserAsync("m5_inactivo", "TECNICO");
                await env.Db.ExecuteAsync($"UPDATE inventario.app_users SET status = 'inactivo' WHERE user_id = {inactive}");
                body = new { expectedVersion = 1, performedBy = inactive };
                break;
            case "tipo_borrado":
                await env.Db.ExecuteAsync($"UPDATE inventario.maintenance_types SET is_deleted = true WHERE maintenance_type_id = {corrective}");
                body = new { expectedVersion = 1, maintenanceTypeId = corrective };
                break;
            case "tipo_inexistente":
                body = new { expectedVersion = 1, maintenanceTypeId = 987_654 };
                break;
            case "activo_borrado":
                await env.SetAssetAsync(asset, "is_deleted = true, deleted_at = now()");
                break;
            case "version_cero":
                body = new { expectedVersion = 0, description = "cambio" };
                break;
        }

        var response = await env.UpdateAsync(id, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        if (caso == "activo_borrado")
        {
            Assert.Contains("eliminado", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        }

        Assert.Equal(1, await env.RecordVersionAsync(id));
        Assert.Equal("original", await env.ScalarOrNullAsync(
            $"SELECT description FROM inventario.maintenance_records WHERE maintenance_record_id = {id}"));
    }

    [DockerFact]
    public async Task Update_DeUnRegistroInexistenteOBorrado_Devuelve404()
    {
        await using var env = await CreateEnvAsync();
        var asset = await env.InsertAssetAsync();
        var deleted = await env.InsertRecordAsync(asset, "PROGRAMADO", scheduledAt: "2030-03-01T09:00:00Z", deleted: true);

        Assert.Equal(HttpStatusCode.NotFound, (await env.UpdateAsync(987_654, new { expectedVersion = 1, description = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await env.UpdateAsync(deleted, new { expectedVersion = 1, description = "x" })).StatusCode);
    }

    // ── A6: carrera Update ‖ Start ─────────────────────────────────────────

    [DockerFact]
    public async Task Carrera_UpdateYStart_DelMismoRegistro_UnoGana_NuncaUn500()
    {
        await using var env = await CreateEnvAsync();
        var failures = new List<string>();

        for (var iteration = 1; iteration <= 12; iteration++)
        {
            var asset = await env.InsertAssetAsync();
            var id = await env.InsertRecordAsync(asset, "PROGRAMADO", scheduledAt: "2030-03-01T09:00:00Z", description: "original");
            var gate = new TaskCompletionSource();

            var update = Task.Run(async () =>
            {
                await gate.Task;
                return (await env.UpdateAsync(id, new { expectedVersion = 1, description = "editada" })).StatusCode;
            });
            var start = Task.Run(async () =>
            {
                await gate.Task;
                return (await env.StartAsync(id, 1)).StatusCode;
            });
            gate.SetResult();
            var codes = await Task.WhenAll(update, start);

            var status = await env.RecordStatusAsync(id);
            var assetStatus = await env.AssetStatusAsync(asset);
            var description = await env.ScalarOrNullAsync(
                $"SELECT description FROM inventario.maintenance_records WHERE maintenance_record_id = {id}");
            var ok = codes.Count(c => c == HttpStatusCode.OK);
            var loserIsClean = codes.Where(c => c != HttpStatusCode.OK).All(c => c is HttpStatusCode.Conflict or HttpStatusCode.BadRequest);

            // Coherencia: si ganó Start el activo está EN_MANTENIMIENTO y la descripción NO cambió
            // (Update perdió); si ganó Update el registro sigue PROGRAMADO y el activo intacto.
            var consistent = (codes[1] == HttpStatusCode.OK && status == "EN_CURSO" && assetStatus == "EN_MANTENIMIENTO" && description == "original")
                          || (codes[0] == HttpStatusCode.OK && status == "PROGRAMADO" && assetStatus == "DISPONIBLE" && description == "editada");

            if (codes.Any(c => (int)c >= 500) || ok != 1 || !loserIsClean || !consistent)
            {
                failures.Add($"iteración {iteration}: [update={codes[0]}, start={codes[1]}], registro={status}, activo={assetStatus}, desc={description}");
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    // ── A7: Delete ─────────────────────────────────────────────────────────

    [DockerTheory]
    [InlineData("PROGRAMADO")]
    [InlineData("COMPLETADO")]
    [InlineData("CANCELADO")]
    public async Task Delete_ProgramadoCompletadoCancelado_Borra_ActivoIntacto_Audita(string status)
    {
        await using var env = await CreateEnvAsync();
        var asset = await env.InsertAssetAsync();
        var id = status switch
        {
            "PROGRAMADO" => await env.InsertRecordAsync(asset, "PROGRAMADO", scheduledAt: "2030-03-01T09:00:00Z"),
            "COMPLETADO" => await env.InsertRecordAsync(asset, "COMPLETADO", startedAt: "2030-01-01T00:00:00Z", performedAt: "2030-01-02T00:00:00Z"),
            _ => await env.InsertRecordAsync(asset, "CANCELADO", scheduledAt: "2030-01-01T00:00:00Z", cancelledAt: "2030-01-02T00:00:00Z", reason: "x")
        };
        var assetVersion = await env.AssetVersionAsync(asset);

        var response = await env.DeleteAsync(id, 1);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await MaintenanceEnv.ReadAsync(response);
        Assert.Equal(id, body.GetProperty("maintenanceRecordId").GetInt64());
        Assert.True(body.GetProperty("deleted").GetBoolean());
        Assert.True(await env.Db.ScalarAsync<bool>(
            $"SELECT is_deleted FROM inventario.maintenance_records WHERE maintenance_record_id = {id}"));
        Assert.Equal(1, await env.AuditCountAsync("maintenance_records", id, "DELETE"));
        Assert.Equal("DISPONIBLE", await env.AssetStatusAsync(asset));
        Assert.Equal(assetVersion, await env.AssetVersionAsync(asset));

        Assert.Equal(HttpStatusCode.NotFound, (await env.GetAsync($"/api/maintenance/{id}")).Status);
        Assert.Empty(MaintenanceEnv.Ids((await env.GetAsync("/api/maintenance")).Body.GetProperty("items")));
        Assert.Equal(HttpStatusCode.NotFound, (await env.DeleteAsync(id, 2)).StatusCode); // ya borrado
    }

    // ── A8: Delete bloqueado ───────────────────────────────────────────────

    [DockerFact]
    public async Task Delete_EnCurso_Rechaza400()
    {
        await using var env = await CreateEnvAsync();
        var asset = await env.InsertAssetAsync();
        var created = await env.StartDirectAsync(asset);
        var body = await MaintenanceEnv.ReadAsync(created);
        var id = body.GetProperty("maintenanceRecordId").GetInt64();

        var response = await env.DeleteAsync(id, body.GetProperty("version").GetInt32());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Cierra o cancela el mantenimiento antes de eliminarlo.", await response.Content.ReadAsStringAsync());
        Assert.False(await env.Db.ScalarAsync<bool>(
            $"SELECT is_deleted FROM inventario.maintenance_records WHERE maintenance_record_id = {id}"));
        Assert.Equal("EN_MANTENIMIENTO", await env.AssetStatusAsync(asset));
    }

    [DockerTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Delete_ConDocumentoAsociado_409_AunSiElDocumentoEstaBorrado(bool documentDeleted)
    {
        await using var env = await CreateEnvAsync(isolatedStorage: true);
        var asset = await env.InsertAssetAsync();
        var id = await env.InsertRecordAsync(asset, "PROGRAMADO", scheduledAt: "2030-03-01T09:00:00Z");
        var upload = await env.UploadDocumentAsync(Pdf, "evidencia.pdf", "application/pdf", id);
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        if (documentDeleted)
        {
            var documentId = (await MaintenanceEnv.ReadAsync(upload)).GetProperty("documentId").GetInt64();
            var removed = await env.SendAsync(HttpMethod.Delete, $"/api/documents/{documentId}?expectedVersion=1");
            Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        }

        var response = await env.DeleteAsync(id, 1);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("El mantenimiento tiene documentos asociados.", await response.Content.ReadAsStringAsync());
        Assert.False(await env.Db.ScalarAsync<bool>(
            $"SELECT is_deleted FROM inventario.maintenance_records WHERE maintenance_record_id = {id}"));
    }

    [DockerFact]
    public async Task Delete_ConVersionVieja_409()
    {
        await using var env = await CreateEnvAsync();
        var asset = await env.InsertAssetAsync();
        var id = await env.InsertRecordAsync(asset, "PROGRAMADO", scheduledAt: "2030-03-01T09:00:00Z");
        await env.Db.ExecuteAsync($"UPDATE inventario.maintenance_records SET description = 'otro' WHERE maintenance_record_id = {id}");

        var response = await env.DeleteAsync(id, 1);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.False(await env.Db.ScalarAsync<bool>(
            $"SELECT is_deleted FROM inventario.maintenance_records WHERE maintenance_record_id = {id}"));
        Assert.Equal(0, await env.AuditCountAsync("maintenance_records", id, "DELETE"));
    }

    [DockerFact]
    public async Task Delete_DeUnRegistroInexistente_404()
    {
        await using var env = await CreateEnvAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await env.DeleteAsync(987_654, 1)).StatusCode);
    }

    // ── A9: permisos ───────────────────────────────────────────────────────

    [DockerFact]
    public async Task Permisos_DeleteSinMaintenanceDelete_403_UpdateSinManage_403()
    {
        await using var env = await CreateEnvAsync();
        var asset = await env.InsertAssetAsync();
        var id = await env.InsertRecordAsync(asset, "PROGRAMADO", scheduledAt: "2030-03-01T09:00:00Z");
        var (tecnico, _) = await env.CreateUserAsync("m5_tec_perm", "TECNICO");
        var (responsable, _) = await env.CreateUserAsync("m5_resp_perm", "RESPONSABLE_AREA");
        var (general, _) = await env.CreateUserAsync("m5_gen_perm", "USUARIO_GENERAL");

        Assert.Equal(HttpStatusCode.Forbidden, (await env.DeleteAsync(id, 1, tecnico)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await env.DeleteAsync(id, 1, responsable)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await env.UpdateAsync(id, new { expectedVersion = 1, description = "x" }, responsable)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await env.UpdateAsync(id, new { expectedVersion = 1, description = "x" }, general)).StatusCode);

        Assert.Equal(1, await env.RecordVersionAsync(id));
        Assert.False(await env.Db.ScalarAsync<bool>(
            $"SELECT is_deleted FROM inventario.maintenance_records WHERE maintenance_record_id = {id}"));

        // El técnico sí puede editar (maintenance.manage).
        Assert.Equal(HttpStatusCode.OK, (await env.UpdateAsync(id, new { expectedVersion = 1, description = "del técnico" }, tecnico)).StatusCode);
    }

    // ── A10: evidencias de extremo a extremo (OwnerType MAINTENANCE) ───────

    [DockerFact]
    public async Task Evidencias_PdfYPng_EnLosCuatroEstados_ListarYDescargar()
    {
        await using var env = await CreateEnvAsync(isolatedStorage: true);
        var records = new Dictionary<string, long>
        {
            ["PROGRAMADO"] = await env.InsertRecordAsync(await env.InsertAssetAsync(), "PROGRAMADO", scheduledAt: "2030-03-01T09:00:00Z"),
            ["EN_CURSO"] = await env.InsertRecordAsync(await env.InsertAssetAsync(), "EN_CURSO", startedAt: "2030-01-01T00:00:00Z"),
            ["COMPLETADO"] = await env.InsertRecordAsync(await env.InsertAssetAsync(), "COMPLETADO", startedAt: "2030-01-01T00:00:00Z", performedAt: "2030-01-02T00:00:00Z"),
            ["CANCELADO"] = await env.InsertRecordAsync(await env.InsertAssetAsync(), "CANCELADO", scheduledAt: "2030-01-01T00:00:00Z", cancelledAt: "2030-01-02T00:00:00Z", reason: "x")
        };

        foreach (var (state, id) in records)
        {
            var pdf = await env.UploadDocumentAsync(Pdf, "informe.pdf", "application/pdf", id);
            // La extensión .jpg y el Content-Type no mandan: decide el contenido (PNG).
            var png = await env.UploadDocumentAsync(Png, "foto.jpg", "image/jpeg", id);
            Assert.True(pdf.StatusCode == HttpStatusCode.Created, $"PDF en {state}: {pdf.StatusCode}");
            Assert.True(png.StatusCode == HttpStatusCode.Created, $"PNG en {state}: {png.StatusCode}");
            var pdfId = (await MaintenanceEnv.ReadAsync(pdf)).GetProperty("documentId").GetInt64();
            var pngId = (await MaintenanceEnv.ReadAsync(png)).GetProperty("documentId").GetInt64();

            var list = await env.GetAsync($"/api/documents?ownerType=MAINTENANCE&ownerId={id}");
            Assert.Equal(HttpStatusCode.OK, list.Status);
            var items = list.Body.EnumerateArray().ToArray();
            Assert.Equal(2, items.Length);
            Assert.Equal(new[] { "application/pdf", "image/png" },
                items.Select(i => i.GetProperty("mimeType").GetString()!).OrderBy(x => x).ToArray());

            var downloadedPdf = await env.DownloadDocumentAsync(pdfId);
            Assert.Equal(HttpStatusCode.OK, downloadedPdf.StatusCode);
            Assert.Equal("application/pdf", downloadedPdf.Content.Headers.ContentType?.MediaType);
            Assert.Equal(Pdf, await downloadedPdf.Content.ReadAsByteArrayAsync());

            var downloadedPng = await env.DownloadDocumentAsync(pngId);
            Assert.Equal(HttpStatusCode.OK, downloadedPng.StatusCode);
            Assert.Equal("image/png", downloadedPng.Content.Headers.ContentType?.MediaType);
            Assert.Equal(Png, await downloadedPng.Content.ReadAsByteArrayAsync());

            // Borrar un registro con documentos -> 409.
            var delete = await env.DeleteAsync(id, 1);
            Assert.True(delete.StatusCode == HttpStatusCode.Conflict || (state == "EN_CURSO" && delete.StatusCode == HttpStatusCode.BadRequest),
                $"Delete en {state}: {delete.StatusCode}");
        }

        // Los archivos están en el storage temporal del test, no en la carpeta real.
        Assert.Equal(8, Directory.GetFiles(env.StorageRoot!).Length);
        Assert.StartsWith(Path.GetTempPath(), env.StorageRoot);
    }

    [DockerFact]
    public async Task Evidencias_RegistroBorradoOActivoBorrado_LaSubidaDa400()
    {
        await using var env = await CreateEnvAsync(isolatedStorage: true);
        var assetA = await env.InsertAssetAsync();
        var deletedRecord = await env.InsertRecordAsync(assetA, "PROGRAMADO", scheduledAt: "2030-03-01T09:00:00Z");
        Assert.Equal(HttpStatusCode.OK, (await env.DeleteAsync(deletedRecord, 1)).StatusCode); // borrado por la API

        var assetB = await env.InsertAssetAsync();
        var recordOfDeletedAsset = await env.InsertRecordAsync(assetB, "PROGRAMADO", scheduledAt: "2030-03-01T09:00:00Z");
        await env.SetAssetAsync(assetB, "is_deleted = true, deleted_at = now()");

        var first = await env.UploadDocumentAsync(Pdf, "informe.pdf", "application/pdf", deletedRecord);
        var second = await env.UploadDocumentAsync(Pdf, "informe.pdf", "application/pdf", recordOfDeletedAsset);
        var third = await env.UploadDocumentAsync(Pdf, "informe.pdf", "application/pdf", 987_654);

        Assert.Equal(HttpStatusCode.BadRequest, first.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, third.StatusCode);
        Assert.Equal(0, await env.Db.CountAsync("documents"));
        Assert.Empty(Directory.Exists(env.StorageRoot!) ? Directory.GetFiles(env.StorageRoot!) : Array.Empty<string>());
    }

    [DockerFact]
    public async Task Evidencias_SinMaintenanceManage_LaSubidaDa403()
    {
        await using var env = await CreateEnvAsync(isolatedStorage: true);
        var asset = await env.InsertAssetAsync();
        var id = await env.InsertRecordAsync(asset, "PROGRAMADO", scheduledAt: "2030-03-01T09:00:00Z");
        var (responsable, _) = await env.CreateUserAsync("m5_resp_doc", "RESPONSABLE_AREA");

        var response = await env.UploadDocumentAsync(Pdf, "informe.pdf", "application/pdf", id, token: responsable);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, await env.Db.CountAsync("documents"));
    }
}
