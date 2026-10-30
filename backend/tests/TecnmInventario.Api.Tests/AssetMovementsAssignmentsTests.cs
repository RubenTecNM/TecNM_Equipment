using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Core.Entities;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Assets.Assignments.CreateAssetAssignment;
using TecnmInventario.Api.Features.Assets.Assignments.EndAssetAssignment;
using TecnmInventario.Api.Features.Assets.Movements.CreateAssetMovement;
using TecnmInventario.Api.Features.Assets.Shared;

namespace TecnmInventario.Api.Tests;

/// <summary>
/// Reglas de negocio de movimientos y asignaciones (máquina de estados,
/// transacción + audit_log). Usa el proveedor en memoria: las transacciones
/// explícitas se ignoran (TransactionIgnoredWarning suprimido), pero las
/// reglas, los saves y las filas de audit_log se ejecutan de verdad.
/// </summary>
public class AssetMovementsAssignmentsTests
{
    private const long LocationA = AssetFlowsHarness.LocationA;
    private const long LocationB = AssetFlowsHarness.LocationB;
    private const long UserId = AssetFlowsHarness.UserId;
    private const long TechnicianId = AssetFlowsHarness.TechnicianId;

    // ---------- Movimientos ----------

    [Fact]
    public async Task Mover_EnMantenimiento_Rechazado()
    {
        using var h = AssetFlowsHarness.CreateInMemory();
        var asset = h.AddAsset(AssetStatusCodes.EnMantenimiento);

        var ex = await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            h.Movement.Handle(new CreateAssetMovementCommand(asset.AssetId, 1, LocationB, 1, null, null), default).AsTask());

        Assert.Contains(AssetStatusCodes.EnMantenimiento, ex.Message);
        Assert.Empty(h.Context.AssetMovements);
    }

    [Fact]
    public async Task Mover_ConFalla_Rechazado()
    {
        using var h = AssetFlowsHarness.CreateInMemory();
        var asset = h.AddAsset(AssetStatusCodes.ConFalla);

        var ex = await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            h.Movement.Handle(new CreateAssetMovementCommand(asset.AssetId, 1, LocationB, 1, null, null), default).AsTask());

        Assert.Contains(AssetStatusCodes.ConFalla, ex.Message);
        Assert.Empty(h.Context.AssetMovements);
    }

    [Fact]
    public async Task Mover_DadoDeBaja_Rechazado()
    {
        using var h = AssetFlowsHarness.CreateInMemory();
        var asset = h.AddAsset(AssetStatusCodes.DadoDeBaja);

        var ex = await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            h.Movement.Handle(new CreateAssetMovementCommand(asset.AssetId, 1, LocationB, 1, null, null), default).AsTask());

        Assert.Contains(AssetStatusCodes.DadoDeBaja, ex.Message);
        Assert.Empty(h.Context.AssetMovements);
    }

    [Fact]
    public async Task Mover_Disponible_Permitido_RegistraMovimientoYAuditoria()
    {
        using var h = AssetFlowsHarness.CreateInMemory();
        var asset = h.AddAsset(AssetStatusCodes.Disponible);

        var result = await h.Movement.Handle(
            new CreateAssetMovementCommand(asset.AssetId, 1, LocationB, 1, null, null), default);

        Assert.Equal(LocationA, result.FromLocationId);
        Assert.Equal(LocationB, result.ToLocationId);

        var movement = h.Context.AssetMovements.Single();
        Assert.Equal(asset.AssetId, movement.AssetId);
        Assert.Equal(LocationA, movement.FromLocationId);
        Assert.Equal(LocationB, movement.ToLocationId);

        var fresh = h.StatusOf(asset.AssetId);
        Assert.Equal(LocationB, fresh.CurrentLocationId);
        Assert.Equal(AssetStatusCodes.Disponible, fresh.AssetStatus.Code);

        var audits = h.Context.AuditLogs.ToList();
        Assert.Contains(audits, a => a.TableName == "asset_movements" && a.Action == "INSERT");
        Assert.Contains(audits, a => a.TableName == "assets" && a.Action == "UPDATE");
    }

    [Fact]
    public async Task Mover_Asignado_Permitido()
    {
        using var h = AssetFlowsHarness.CreateInMemory();
        var asset = h.AddAsset(AssetStatusCodes.Asignado);

        await h.Movement.Handle(new CreateAssetMovementCommand(asset.AssetId, 1, LocationB, 1, null, null), default);

        Assert.Equal(LocationB, h.StatusOf(asset.AssetId).CurrentLocationId);
    }

    [Fact]
    public async Task Mover_MismoDestino_Rechazado()
    {
        using var h = AssetFlowsHarness.CreateInMemory();
        var asset = h.AddAsset(AssetStatusCodes.Disponible);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            h.Movement.Handle(new CreateAssetMovementCommand(asset.AssetId, 1, LocationA, 1, null, null), default).AsTask());
    }

    // ---------- Asignaciones ----------

    [Fact]
    public async Task Asignar_NoDisponible_Rechazado()
    {
        using var h = AssetFlowsHarness.CreateInMemory();
        var asset = h.AddAsset(AssetStatusCodes.EnMantenimiento);

        var ex = await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            h.Assign.Handle(new CreateAssetAssignmentCommand(asset.AssetId, 1, TechnicianId, 1, LocationA, null, null), default).AsTask());

        Assert.Contains(AssetStatusCodes.Disponible, ex.Message);
        Assert.Empty(h.Context.AssetAssignments);
    }

    [Fact]
    public async Task Asignar_ConAsignacionActiva_Rechazado()
    {
        using var h = AssetFlowsHarness.CreateInMemory();
        var asset = h.AddAsset(AssetStatusCodes.Disponible);
        h.AddAssignment(asset.AssetId);

        var ex = await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            h.Assign.Handle(new CreateAssetAssignmentCommand(asset.AssetId, 1, TechnicianId, 1, LocationA, null, null), default).AsTask());

        Assert.Contains("asignación activa", ex.Message);
        Assert.Single(h.Context.AssetAssignments);
    }

    [Fact]
    public async Task Devolver_ConIncidenteAbierto_ConservaEstado()
    {
        using var h = AssetFlowsHarness.CreateInMemory();
        var asset = h.AddAsset(AssetStatusCodes.Asignado);
        var assignment = h.AddAssignment(asset.AssetId);
        h.AddIncident(asset.AssetId, IncidentStatusCodes.Abierto);

        await h.Return.Handle(new EndAssetAssignmentCommand(asset.AssetId, 1, assignment.AssetAssignmentId), default);

        Assert.Equal(AssetStatusCodes.Asignado, h.StatusOf(asset.AssetId).AssetStatus.Code);

        var returned = h.Context.AssetAssignments.AsNoTracking().Single(a => a.AssetAssignmentId == assignment.AssetAssignmentId);
        Assert.NotNull(returned.ReturnedAt);
    }

    [Fact]
    public async Task Devolver_EnOtroEstado_ConservaEstado()
    {
        using var h = AssetFlowsHarness.CreateInMemory();
        var asset = h.AddAsset(AssetStatusCodes.ConFalla);
        var assignment = h.AddAssignment(asset.AssetId);

        await h.Return.Handle(new EndAssetAssignmentCommand(asset.AssetId, 1, assignment.AssetAssignmentId), default);

        Assert.Equal(AssetStatusCodes.ConFalla, h.StatusOf(asset.AssetId).AssetStatus.Code);
    }

    // ---------- Concurrencia optimista (expectedVersion) ----------

    [Fact]
    public async Task Mover_VersionDesactualizada_Rechazado_SinFilas()
    {
        using var h = AssetFlowsHarness.CreateInMemory();
        var asset = h.AddAsset(AssetStatusCodes.Disponible);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            h.Movement.Handle(
                new CreateAssetMovementCommand(asset.AssetId, 99, LocationB, 1, null, null), default).AsTask());

        Assert.Empty(h.Context.AssetMovements);
        Assert.Empty(h.Context.AuditLogs);
    }

    [Fact]
    public async Task Mover_VersionCorrecta_Exito_DevuelveVersionDelActivo()
    {
        using var h = AssetFlowsHarness.CreateInMemory();
        var asset = h.AddAsset(AssetStatusCodes.Disponible);

        var result = await h.Movement.Handle(
            new CreateAssetMovementCommand(asset.AssetId, asset.Version, LocationB, 1, null, null), default);

        var fresh = await h.Context.Assets
            .AsNoTracking()
            .SingleAsync(a => a.AssetId == asset.AssetId);
        Assert.Equal(fresh.Version, result.AssetVersion);
        Assert.Equal(LocationB, fresh.CurrentLocationId);
    }

    [Fact]
    public async Task Asignar_VersionDesactualizada_Rechazado_SinFilas()
    {
        using var h = AssetFlowsHarness.CreateInMemory();
        var asset = h.AddAsset(AssetStatusCodes.Disponible);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            h.Assign.Handle(
                new CreateAssetAssignmentCommand(asset.AssetId, 99, TechnicianId, 1, LocationA, null, null), default).AsTask());

        Assert.Empty(h.Context.AssetAssignments);
        Assert.Empty(h.Context.AuditLogs);
    }

    [Fact]
    public async Task Devolver_VersionDesactualizada_Rechazado_SinFilas()
    {
        using var h = AssetFlowsHarness.CreateInMemory();
        var asset = h.AddAsset(AssetStatusCodes.Asignado);
        var assignment = h.AddAssignment(asset.AssetId);
        // Incidente abierto: el activo NO se UPDATEa en esta operación,
        // así que solo la comparación explícita detecta la versión vieja.
        h.AddIncident(asset.AssetId, IncidentStatusCodes.Abierto);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            h.Return.Handle(
                new EndAssetAssignmentCommand(asset.AssetId, 99, assignment.AssetAssignmentId), default).AsTask());

        var returned = await h.Context.AssetAssignments.AsNoTracking()
            .SingleAsync(a => a.AssetAssignmentId == assignment.AssetAssignmentId);
        Assert.Null(returned.ReturnedAt);
        Assert.Empty(h.Context.AuditLogs);
    }

    [Fact]
    public async Task Devolver_VersionCorrecta_Exito_DevuelveVersionDelActivo()
    {
        using var h = AssetFlowsHarness.CreateInMemory();
        var asset = h.AddAsset(AssetStatusCodes.Asignado);
        var assignment = h.AddAssignment(asset.AssetId);
        // Incidente abierto: la devolución conserva ASIGNADO (no pasa
        // por AssetStatusChanger), con lo que la operación completa es
        // ejecutable con el proveedor InMemory. El camino normal
        // (ASIGNADO -> DISPONIBLE) se verifica contra PostgreSQL con
        // curl en la prueba real del bloque 2.
        h.AddIncident(asset.AssetId, IncidentStatusCodes.Abierto);

        var result = await h.Return.Handle(
            new EndAssetAssignmentCommand(asset.AssetId, asset.Version, assignment.AssetAssignmentId), default);

        Assert.Equal(
            (await h.Context.Assets.AsNoTracking().SingleAsync(a => a.AssetId == asset.AssetId)).Version,
            result.AssetVersion);
        Assert.Equal(AssetStatusCodes.Asignado, h.StatusOf(asset.AssetId).AssetStatus.Code);
        var returned = await h.Context.AssetAssignments.AsNoTracking()
            .SingleAsync(a => a.AssetAssignmentId == assignment.AssetAssignmentId);
        Assert.NotNull(returned.ReturnedAt);
    }
}
