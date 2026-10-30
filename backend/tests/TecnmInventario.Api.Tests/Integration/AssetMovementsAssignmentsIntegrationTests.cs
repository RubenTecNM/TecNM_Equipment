using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Features.Assets.Assignments.CreateAssetAssignment;
using TecnmInventario.Api.Features.Assets.Assignments.EndAssetAssignment;

namespace TecnmInventario.Api.Tests.Integration;

/// <summary>
/// Los cuatro flujos de movimientos/asignaciones que antes estaban omitidos
/// (transacciones reales de PostgreSQL). Las aserciones son las mismas que
/// usaba la suite InMemory; solo cambia la base de datos.
/// </summary>
[Trait("Category", "Integration")]
public sealed class AssetMovementsAssignmentsIntegrationTests : PostgresTestBase
{
    public AssetMovementsAssignmentsIntegrationTests(PostgresContainerFixture fixture)
        : base(fixture)
    {
    }

    [DockerFact]
    public async Task Asignar_DejaAsignado_RegistraAuditoria()
    {
        await using var db = await CreateDatabaseAsync();
        using var h = await AssetFlowsHarness.CreatePostgresAsync(db);
        var asset = h.AddAsset(AssetStatusCodes.Disponible);

        await h.Assign.Handle(
            new CreateAssetAssignmentCommand(
                asset.AssetId, 1, AssetFlowsHarness.TechnicianId, 1, AssetFlowsHarness.LocationA, null, null),
            default);

        var fresh = h.StatusOf(asset.AssetId);
        Assert.Equal(AssetStatusCodes.Asignado, fresh.AssetStatus.Code);

        var assignment = h.Context.AssetAssignments.Single();
        Assert.Null(assignment.ReturnedAt);
        Assert.Equal(AssetFlowsHarness.TechnicianId, assignment.AssignedTo);

        var audits = h.Context.AuditLogs.ToList();
        Assert.Contains(audits, a => a.TableName == "asset_assignments" && a.Action == "INSERT");
        Assert.Contains(audits, a => a.TableName == "assets" && a.Action == "UPDATE"
            && a.NewData != null && a.NewData.Contains("\"Status\":\"ASIGNADO\"", StringComparison.Ordinal));

        // El trigger touch_version subió la versión en la base real.
        Assert.True(fresh.Version > 1);
    }

    [DockerFact]
    public async Task Devolver_DejaDisponible_RegistraAuditoria()
    {
        await using var db = await CreateDatabaseAsync();
        using var h = await AssetFlowsHarness.CreatePostgresAsync(db);
        var asset = h.AddAsset(AssetStatusCodes.Asignado);
        var assignment = h.AddAssignment(asset.AssetId);

        await h.Return.Handle(
            new EndAssetAssignmentCommand(asset.AssetId, 1, assignment.AssetAssignmentId), default);

        Assert.Equal(AssetStatusCodes.Disponible, h.StatusOf(asset.AssetId).AssetStatus.Code);

        var returned = h.Context.AssetAssignments.AsNoTracking()
            .Single(a => a.AssetAssignmentId == assignment.AssetAssignmentId);
        Assert.NotNull(returned.ReturnedAt);

        var audits = h.Context.AuditLogs.ToList();
        Assert.Contains(audits, a => a.TableName == "asset_assignments" && a.Action == "UPDATE");
        Assert.Contains(audits, a => a.TableName == "assets" && a.Action == "UPDATE"
            && a.NewData != null && a.NewData.Contains(AssetStatusCodes.Disponible));
    }

    [DockerFact]
    public async Task Devolver_SegundaVez_Rechazado()
    {
        await using var db = await CreateDatabaseAsync();
        using var h = await AssetFlowsHarness.CreatePostgresAsync(db);
        var asset = h.AddAsset(AssetStatusCodes.Asignado);
        var assignment = h.AddAssignment(asset.AssetId);

        await h.Return.Handle(
            new EndAssetAssignmentCommand(asset.AssetId, 1, assignment.AssetAssignmentId), default);

        // Releer la versión tras la primera devolución: el reintento con
        // versión fresca debe caer en la regla de negocio (400), no en 409.
        var freshVersion = h.Context.Assets.AsNoTracking()
            .Single(a => a.AssetId == asset.AssetId).Version;
        var ex = await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            h.Return.Handle(
                new EndAssetAssignmentCommand(asset.AssetId, freshVersion, assignment.AssetAssignmentId),
                default).AsTask());

        Assert.Contains("ya fue devuelta", ex.Message);
    }

    [DockerFact]
    public async Task Asignar_VersionCorrecta_Exito_DevuelveVersionDelActivo()
    {
        await using var db = await CreateDatabaseAsync();
        using var h = await AssetFlowsHarness.CreatePostgresAsync(db);
        var asset = h.AddAsset(AssetStatusCodes.Disponible);

        var result = await h.Assign.Handle(
            new CreateAssetAssignmentCommand(
                asset.AssetId, asset.Version, AssetFlowsHarness.TechnicianId, 1,
                AssetFlowsHarness.LocationA, null, null),
            default);

        Assert.Equal(
            (await h.Context.Assets.AsNoTracking().SingleAsync(a => a.AssetId == asset.AssetId)).Version,
            result.AssetVersion);
        Assert.Equal(AssetStatusCodes.Asignado, h.StatusOf(asset.AssetId).AssetStatus.Code);
    }
}
