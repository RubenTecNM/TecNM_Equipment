namespace TecnmInventario.Api.Tests.Integration;

/// <summary>
/// M.1: permisos del módulo (scripts/add-maintenance-permissions.sql).
/// maintenance.view: ADMIN, TECNICO y RESPONSABLE_AREA. maintenance.delete: solo
/// ADMIN. maintenance.manage (ya existía): ADMIN y TECNICO.
/// </summary>
[Trait("Category", "Integration")]
public sealed class MaintenancePermissionsTests : PostgresTestBase
{
    private const string ScriptName = "add-maintenance-permissions.sql";

    public MaintenancePermissionsTests(PostgresContainerFixture fixture)
        : base(fixture) { }

    [DockerFact]
    public async Task Permisos_SeAsignanALosRolesEsperados()
    {
        await using var db = await CreateDatabaseAsync();

        Assert.Equal(
            ["ADMIN", "RESPONSABLE_AREA", "TECNICO"],
            await RolesWithAsync(db, "maintenance.view"));
        Assert.Equal(["ADMIN"], await RolesWithAsync(db, "maintenance.delete"));
        Assert.Equal(["ADMIN", "TECNICO"], await RolesWithAsync(db, "maintenance.manage"));
    }

    [DockerFact]
    public async Task Script_EsIdempotente_SinDuplicarPermisosNiAsignaciones()
    {
        await using var db = await CreateDatabaseAsync();
        var sql = await ReadScriptAsync();

        var permissionsBefore = await db.CountAsync("permissions", "code LIKE 'maintenance.%'");
        var assignmentsBefore = await db.CountAsync("role_permissions", """
            permission_id IN (SELECT permission_id FROM inventario.permissions WHERE code LIKE 'maintenance.%')
            """);

        await db.ExecuteAsync(sql);
        await db.ExecuteAsync(sql);

        Assert.Equal(3, permissionsBefore); // view, manage, delete
        Assert.Equal(permissionsBefore, await db.CountAsync("permissions", "code LIKE 'maintenance.%'"));
        Assert.Equal(assignmentsBefore, await db.CountAsync("role_permissions", """
            permission_id IN (SELECT permission_id FROM inventario.permissions WHERE code LIKE 'maintenance.%')
            """));
        // 3 (view) + 1 (delete) + 2 (manage)
        Assert.Equal(6, assignmentsBefore);
    }

    private static async Task<string[]> RolesWithAsync(PostgresDatabase db, string permissionCode)
    {
        await using var connection = await db.OpenConnectionAsync();
        await using var command = new Npgsql.NpgsqlCommand($"""
            SELECT r.code
            FROM inventario.role_permissions rp
            JOIN inventario.roles r ON r.role_id = rp.role_id
            JOIN inventario.permissions p ON p.permission_id = rp.permission_id
            WHERE p.code = '{permissionCode}'
            ORDER BY r.code
            """, connection);
        var roles = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            roles.Add(reader.GetString(0));
        }

        return [.. roles];
    }

    private static async Task<string> ReadScriptAsync()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Sql", ScriptName);
        Assert.True(File.Exists(path), $"No existe el script enlazado en el proyecto de tests: {path}");
        return await File.ReadAllTextAsync(path);
    }
}
