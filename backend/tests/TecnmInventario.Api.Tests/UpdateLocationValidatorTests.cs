using TecnmInventario.Api.Core.Entities;
using TecnmInventario.Api.Features.Locations.UpdateLocation;

namespace TecnmInventario.Api.Tests;

public class UpdateLocationValidatorTests
{
    private static TecnmInventario.Api.Data.TecnmInventarioDbContext CreateContext()
    {
        var context = TestDb.Create();
        var now = DateTime.UtcNow;

        context.LocationTypes.Add(new LocationType
        {
            LocationTypeId = 1,
            Code = "EDIFICIO",
            Name = "Edificio",
            CreatedAt = now,
            UpdatedAt = now,
            Version = 1
        });
        context.Locations.AddRange(
            new Location
            {
                LocationId = 1, Code = "ED-A", Name = "Edificio A", LocationTypeId = 1,
                CreatedAt = now, UpdatedAt = now, Version = 1
            },
            new Location
            {
                LocationId = 2, Code = "ED-A-P1", Name = "Planta 1", LocationTypeId = 1,
                ParentLocationId = 1, CreatedAt = now, UpdatedAt = now, Version = 1
            },
            new Location
            {
                LocationId = 3, Code = "ED-BORRADA", Name = "Borrada", LocationTypeId = 1,
                CreatedAt = now, UpdatedAt = now, Version = 1, IsDeleted = true
            });
        context.SaveChanges();
        return context;
    }

    [Fact]
    public async Task Parent_inexistente_es_error_de_validacion()
    {
        using var context = CreateContext();
        var validator = new UpdateLocationValidator(context);

        var result = await validator.ValidateAsync(
            new UpdateLocationCommand(2, 1, "Planta 1", 1, 999999));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors,
            e => e.PropertyName == nameof(UpdateLocationCommand.ParentLocationId));
    }

    [Fact]
    public async Task Parent_soft_deleted_es_error_de_validacion()
    {
        using var context = CreateContext();
        var validator = new UpdateLocationValidator(context);

        var result = await validator.ValidateAsync(
            new UpdateLocationCommand(2, 1, "Planta 1", 1, 3));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors,
            e => e.PropertyName == nameof(UpdateLocationCommand.ParentLocationId));
    }

    [Fact]
    public async Task Parent_existente_y_activo_es_valido()
    {
        using var context = CreateContext();
        var validator = new UpdateLocationValidator(context);

        var result = await validator.ValidateAsync(
            new UpdateLocationCommand(2, 1, "Planta 1", 1, 1));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ExpectedVersion_no_positiva_es_error_de_validacion(int expectedVersion)
    {
        using var context = CreateContext();
        var validator = new UpdateLocationValidator(context);

        var result = await validator.ValidateAsync(
            new UpdateLocationCommand(2, expectedVersion, "Planta 1", 1, 1));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors,
            e => e.PropertyName == nameof(UpdateLocationCommand.ExpectedVersion));
    }
}
