using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Core.Entities;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Assets.Assignments.CreateAssetAssignment;
using TecnmInventario.Api.Features.Assets.Assignments.EndAssetAssignment;
using TecnmInventario.Api.Features.Assets.Movements.CreateAssetMovement;
using TecnmInventario.Api.Features.Assets.Shared;
using TecnmInventario.Api.Tests.Integration;

namespace TecnmInventario.Api.Tests;

/// <summary>
/// Arnés compartido por los tests InMemory y los de PostgreSQL de los flujos
/// de movimientos y asignaciones. Solo cambia la fuente del DbContext y el
/// sembrado de catálogos; los handlers y las aserciones son los mismos.
/// </summary>
internal sealed class AssetFlowsHarness : IDisposable
{
    public const long LocationA = 1;
    public const long LocationB = 2;
    public const long UserId = 1;
    public const long TechnicianId = 2;

    private readonly FakeCurrentUser _currentUser = new();
    private readonly IAuditLogService _auditLog;
    private readonly IAssetStatusChanger _statusChanger;

    private AssetFlowsHarness(TecnmInventarioDbContext context, bool ownsContext)
    {
        Context = context;
        OwnsContext = ownsContext;
        _auditLog = new AuditLogService(Context, _currentUser);
        _statusChanger = new AssetStatusChanger(Context, _currentUser, _auditLog);
        Movement = new CreateAssetMovementHandler(Context, _currentUser, _auditLog);
        Assign = new CreateAssetAssignmentHandler(Context, _currentUser, _auditLog, _statusChanger);
        Return = new EndAssetAssignmentHandler(Context, _currentUser, _auditLog, _statusChanger);
    }

    public TecnmInventarioDbContext Context { get; }

    private bool OwnsContext { get; }

    public CreateAssetMovementHandler Movement { get; }

    public CreateAssetAssignmentHandler Assign { get; }

    public EndAssetAssignmentHandler Return { get; }

    /// <summary>Variante rápida: proveedor InMemory con catálogos sembrados.</summary>
    public static AssetFlowsHarness CreateInMemory()
    {
        var options = new DbContextOptionsBuilder<TecnmInventarioDbContext>()
            .UseInMemoryDatabase($"tecnm-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var context = new TecnmInventarioDbContext(options);
        SeedInMemory(context);
        context.SaveChanges();
        return new AssetFlowsHarness(context, ownsContext: true);
    }

    /// <summary>
    /// Variante PostgreSQL: usa los catálogos reales del seed y crea solo los
    /// datos que los flujos necesitan (ubicaciones 1/2 y técnico 2). Se
    /// reutilizan los IDs fijos para no duplicar las aserciones.
    /// </summary>
    public static async Task<AssetFlowsHarness> CreatePostgresAsync(PostgresDatabase database)
    {
        await database.ExecuteAsync(PostgresSeedSql);
        return new AssetFlowsHarness(database.CreateContext(), ownsContext: true);
    }

    public Asset AddAsset(string statusCode)
    {
        var status = Context.AssetStatuses.Single(s => s.Code == statusCode);
        var asset = new Asset
        {
            InventoryCode = $"TEC-TEST-{Guid.NewGuid():N}"[..18],
            AssetTypeId = 1,
            AssetStatusId = status.AssetStatusId,
            CurrentLocationId = LocationA,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Version = 1
        };
        Context.Assets.Add(asset);
        Context.SaveChanges();
        return asset;
    }

    public AssetAssignment AddAssignment(long assetId)
    {
        var assignment = new AssetAssignment
        {
            AssetId = assetId,
            AssignedTo = TechnicianId,
            AssignmentTypeId = 1,
            LocationId = LocationA,
            AssignedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Version = 1
        };
        Context.AssetAssignments.Add(assignment);
        Context.SaveChanges();
        return assignment;
    }

    public void AddIncident(long assetId, string statusCode)
    {
        var status = Context.IncidentStatuses.Single(s => s.Code == statusCode);
        Context.Incidents.Add(new Incident
        {
            AssetId = assetId,
            IncidentTypeId = 1,
            IncidentPriorityId = 1,
            IncidentStatusId = status.IncidentStatusId,
            ReportedBy = UserId,
            Title = "Falla de prueba",
            ReportedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Version = 1
        });
        Context.SaveChanges();
    }

    public Asset StatusOf(long assetId)
        => Context.Assets
            .AsNoTracking()
            .Include(a => a.AssetStatus)
            .Single(a => a.AssetId == assetId);

    public void Dispose()
    {
        if (OwnsContext)
        {
            Context.Dispose();
        }
    }

    private static void SeedInMemory(TecnmInventarioDbContext context)
    {
        var now = DateTime.UtcNow;
        context.AddRange(
            new AssetStatus { AssetStatusId = 1, Code = AssetStatusCodes.Disponible, Name = "Disponible", CreatedAt = now, UpdatedAt = now, Version = 1 },
            new AssetStatus { AssetStatusId = 2, Code = AssetStatusCodes.Asignado, Name = "Asignado", CreatedAt = now, UpdatedAt = now, Version = 1 },
            new AssetStatus { AssetStatusId = 3, Code = AssetStatusCodes.EnMantenimiento, Name = "En mantenimiento", CreatedAt = now, UpdatedAt = now, Version = 1 },
            new AssetStatus { AssetStatusId = 4, Code = AssetStatusCodes.ConFalla, Name = "Con falla", CreatedAt = now, UpdatedAt = now, Version = 1 },
            new AssetStatus { AssetStatusId = 5, Code = AssetStatusCodes.DadoDeBaja, Name = "Dado de baja", CreatedAt = now, UpdatedAt = now, Version = 1 },
            new AssetType { AssetTypeId = 1, Code = "PC", Name = "Equipo de cómputo", CreatedAt = now, UpdatedAt = now, Version = 1 },
            new Location { LocationId = LocationA, Code = "ALM", Name = "Almacén", LocationTypeId = 1, CreatedAt = now, UpdatedAt = now, Version = 1 },
            new Location { LocationId = LocationB, Code = "LAB", Name = "Laboratorio", LocationTypeId = 1, CreatedAt = now, UpdatedAt = now, Version = 1 },
            new MovementReason { MovementReasonId = 1, Code = "REUB", Name = "Reubicación", CreatedAt = now, UpdatedAt = now, Version = 1 },
            new AssignmentType { AssignmentTypeId = 1, Code = "RES", Name = "Resguardo", CreatedAt = now, UpdatedAt = now, Version = 1 },
            new AppUser { UserId = UserId, Username = "admin", Email = "admin@test.local", PasswordHash = "x", FirstName = "Admin", LastName = "Sis", RoleId = 1, Status = "activo", CreatedAt = now, UpdatedAt = now, Version = 1 },
            new AppUser { UserId = TechnicianId, Username = "tec", Email = "tec@test.local", PasswordHash = "x", FirstName = "Técnico", LastName = "Uno", RoleId = 2, Status = "activo", CreatedAt = now, UpdatedAt = now, Version = 1 },
            new IncidentType { IncidentTypeId = 1, Code = "HARD", Name = "Hardware", CreatedAt = now, UpdatedAt = now, Version = 1 },
            new IncidentPriority { IncidentPriorityId = 1, Code = "ALTA", Name = "Alta", CreatedAt = now, UpdatedAt = now, Version = 1 },
            new IncidentStatus { IncidentStatusId = 1, Code = IncidentStatusCodes.Abierto, Name = "Abierto", CreatedAt = now, UpdatedAt = now, Version = 1 },
            new IncidentStatus { IncidentStatusId = 2, Code = IncidentStatusCodes.Resuelto, Name = "Resuelto", CreatedAt = now, UpdatedAt = now, Version = 1 });
    }

    /// <summary>
    /// Datos mínimos sobre el esquema real. Los catálogos (tipos, estados,
    /// motivos) vienen de scripts/seed-database.sql y database.sql.
    /// </summary>
    private const string PostgresSeedSql = """
        INSERT INTO inventario.locations (location_id, code, name, location_type_id)
        OVERRIDING SYSTEM VALUE
        SELECT 1, 'INT-ALM', 'Almacén integración', lt.location_type_id
        FROM inventario.location_types lt WHERE lt.code = 'OFICINA'
        ON CONFLICT (location_id) DO NOTHING;

        INSERT INTO inventario.locations (location_id, code, name, location_type_id)
        OVERRIDING SYSTEM VALUE
        SELECT 2, 'INT-LAB', 'Laboratorio integración', lt.location_type_id
        FROM inventario.location_types lt WHERE lt.code = 'LABORATORIO'
        ON CONFLICT (location_id) DO NOTHING;

        SELECT setval(
            pg_get_serial_sequence('inventario.locations', 'location_id'),
            (SELECT MAX(location_id) FROM inventario.locations));

        INSERT INTO inventario.app_users
            (user_id, username, email, password_hash, first_name, last_name, role_id, status)
        OVERRIDING SYSTEM VALUE
        SELECT 2, 'tec_integracion', 'tec-integracion@test.local', 'NO_LOGIN',
               'Técnico', 'Integración', r.role_id, 'activo'
        FROM inventario.roles r WHERE r.code = 'TECNICO'
        ON CONFLICT (user_id) DO NOTHING;

        SELECT setval(
            pg_get_serial_sequence('inventario.app_users', 'user_id'),
            (SELECT MAX(user_id) FROM inventario.app_users));
        """;
}
