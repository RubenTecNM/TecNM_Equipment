using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Features.Assets.Assignments.EndAssetAssignment;

namespace TecnmInventario.Api.Tests.Integration;

/// <summary>
/// EndAssetAssignment contra PostgreSQL real. Estos casos vivían en InMemory,
/// pero el handler toma un lock de fila (SELECT ... FOR UPDATE) y ese proveedor
/// no ejecuta SQL relacional. La cobertura se conserva íntegra y se añade el
/// camino normal ASIGNADO -> DISPONIBLE.
/// </summary>
[Trait("Category", "Integration")]
public sealed class EndAssetAssignmentPostgresTests : PostgresTestBase
{
    public EndAssetAssignmentPostgresTests(PostgresContainerFixture fixture)
        : base(fixture)
    {
    }

    [DockerFact]
    public async Task Devolver_ConIncidenteAbierto_ConservaEstado()
    {
        await using var db = await CreateDatabaseAsync();
        using var h = await AssetFlowsHarness.CreatePostgresAsync(db);
        var asset = h.AddAsset(AssetStatusCodes.Asignado);
        var assignment = h.AddAssignment(asset.AssetId);
        h.AddIncident(asset.AssetId, IncidentStatusCodes.Abierto);

        await h.Return.Handle(
            new EndAssetAssignmentCommand(asset.AssetId, asset.Version, assignment.AssetAssignmentId),
            default);

        Assert.Equal(AssetStatusCodes.Asignado, h.StatusOf(asset.AssetId).AssetStatus.Code);

        var returned = h.Context.AssetAssignments.AsNoTracking()
            .Single(a => a.AssetAssignmentId == assignment.AssetAssignmentId);
        Assert.NotNull(returned.ReturnedAt);
    }

    [DockerFact]
    public async Task Devolver_EnOtroEstado_ConservaEstado()
    {
        await using var db = await CreateDatabaseAsync();
        using var h = await AssetFlowsHarness.CreatePostgresAsync(db);
        var asset = h.AddAsset(AssetStatusCodes.ConFalla);
        var assignment = h.AddAssignment(asset.AssetId);

        await h.Return.Handle(
            new EndAssetAssignmentCommand(asset.AssetId, asset.Version, assignment.AssetAssignmentId),
            default);

        Assert.Equal(AssetStatusCodes.ConFalla, h.StatusOf(asset.AssetId).AssetStatus.Code);
    }

    [DockerFact]
    public async Task Devolver_AsignadoSinIncidentes_PasaADisponible()
    {
        await using var db = await CreateDatabaseAsync();
        using var h = await AssetFlowsHarness.CreatePostgresAsync(db);
        var asset = h.AddAsset(AssetStatusCodes.Asignado);
        var assignment = h.AddAssignment(asset.AssetId);

        await h.Return.Handle(
            new EndAssetAssignmentCommand(asset.AssetId, asset.Version, assignment.AssetAssignmentId),
            default);

        Assert.Equal(AssetStatusCodes.Disponible, h.StatusOf(asset.AssetId).AssetStatus.Code);
    }

    [DockerFact]
    public async Task Devolver_VersionDesactualizada_Rechazado_SinFilas()
    {
        await using var db = await CreateDatabaseAsync();
        using var h = await AssetFlowsHarness.CreatePostgresAsync(db);
        var asset = h.AddAsset(AssetStatusCodes.Asignado);
        var assignment = h.AddAssignment(asset.AssetId);
        h.AddIncident(asset.AssetId, IncidentStatusCodes.Abierto);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            h.Return.Handle(
                new EndAssetAssignmentCommand(asset.AssetId, 99, assignment.AssetAssignmentId),
                default).AsTask());

        var returned = await h.Context.AssetAssignments.AsNoTracking()
            .SingleAsync(a => a.AssetAssignmentId == assignment.AssetAssignmentId);
        Assert.Null(returned.ReturnedAt);
        Assert.Empty(h.Context.AuditLogs);
    }

    [DockerFact]
    public async Task Devolver_VersionCorrecta_Exito_DevuelveVersionDelActivo()
    {
        await using var db = await CreateDatabaseAsync();
        using var h = await AssetFlowsHarness.CreatePostgresAsync(db);
        var asset = h.AddAsset(AssetStatusCodes.Asignado);
        var assignment = h.AddAssignment(asset.AssetId);
        // Incidente abierto: la devolución conserva ASIGNADO (no pasa por
        // AssetStatusChanger). El camino normal se cubre aparte.
        h.AddIncident(asset.AssetId, IncidentStatusCodes.Abierto);

        var result = await h.Return.Handle(
            new EndAssetAssignmentCommand(asset.AssetId, asset.Version, assignment.AssetAssignmentId),
            default);

        Assert.Equal(
            (await h.Context.Assets.AsNoTracking().SingleAsync(a => a.AssetId == asset.AssetId)).Version,
            result.AssetVersion);
        Assert.Equal(AssetStatusCodes.Asignado, h.StatusOf(asset.AssetId).AssetStatus.Code);

        var returned = await h.Context.AssetAssignments.AsNoTracking()
            .SingleAsync(a => a.AssetAssignmentId == assignment.AssetAssignmentId);
        Assert.NotNull(returned.ReturnedAt);
    }
}
