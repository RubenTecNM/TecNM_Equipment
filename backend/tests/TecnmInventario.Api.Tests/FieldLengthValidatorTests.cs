using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Core.Entities;
using TecnmInventario.Api.Features.Assets.CreateAsset;
using TecnmInventario.Api.Features.Auth.Register;
using TecnmInventario.Api.Features.Documents;
using TecnmInventario.Api.Features.Documents.UploadDocument;
using TecnmInventario.Api.Features.Procurement.Acquisitions.CreateAcquisition;
using TecnmInventario.Api.Features.Procurement.Suppliers.CreateSupplier;
using TecnmInventario.Api.Features.Procurement.Suppliers.UpdateSupplier;
using TecnmInventario.Api.Features.Procurement.Warranties.CreateAssetWarranty;
using TecnmInventario.Api.Features.Roles.CreateRole;

namespace TecnmInventario.Api.Tests;

public class FieldLengthValidatorTests
{
    private sealed class FakeFormFile : IFormFile
    {
        public string ContentType { get; init; } = "application/pdf";
        public string ContentDisposition { get; init; } = "form-data; name=\"file\"";
        public IHeaderDictionary Headers { get; init; } = new HeaderDictionary();
        public long Length { get; init; } = 1;
        public string Name { get; init; } = "file";
        public string FileName { get; init; } = "archivo.pdf";

        public Stream OpenReadStream() => Stream.Null;

        public void CopyTo(Stream target)
        {
        }

        public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private static void AssertHasError(ValidationResult result, string propertyName)
    {
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == propertyName);
    }

    private static void AssertNoError(ValidationResult result, string propertyName)
    {
        Assert.DoesNotContain(result.Errors, e => e.PropertyName == propertyName);
    }

    private static TecnmInventario.Api.Data.TecnmInventarioDbContext CreateContext()
    {
        var context = TestDb.Create();
        var now = DateTime.UtcNow;

        context.AssetTypes.Add(new AssetType
        {
            AssetTypeId = 1, Code = "PC", Name = "Equipo de cómputo",
            CreatedAt = now, UpdatedAt = now, Version = 1
        });
        context.Roles.Add(new Role
        {
            RoleId = 1, Code = "ADMIN", Name = "Administrador",
            CreatedAt = now, UpdatedAt = now, Version = 1
        });
        context.AssetStatuses.Add(new AssetStatus
        {
            AssetStatusId = 1, Code = "DISPONIBLE", Name = "Disponible",
            CreatedAt = now, UpdatedAt = now, Version = 1
        });
        context.Assets.Add(new Asset
        {
            AssetId = 1, InventoryCode = "TEC-1", AssetTypeId = 1, AssetStatusId = 1,
            CreatedAt = now, UpdatedAt = now, Version = 1
        });
        context.SaveChanges();
        return context;
    }

    [Fact]
    public async Task CreateAsset_alinea_inventory_code_con_la_columna()
    {
        using var context = CreateContext();
        var validator = new CreateAssetValidator(context);

        var atLimit = await validator.ValidateAsync(new CreateAssetCommand(
            new string('A', DbFieldLengths.Assets.InventoryCode),
            AssetTypeId: 1, ManufacturerId: null, ModelId: null, CurrentLocationId: null,
            SerialNumber: null, Hostname: null, Notes: null));
        AssertNoError(atLimit, nameof(CreateAssetCommand.InventoryCode));

        var overLimit = await validator.ValidateAsync(new CreateAssetCommand(
            new string('A', DbFieldLengths.Assets.InventoryCode + 1),
            AssetTypeId: 1, ManufacturerId: null, ModelId: null, CurrentLocationId: null,
            SerialNumber: null, Hostname: null, Notes: null));
        AssertHasError(overLimit, nameof(CreateAssetCommand.InventoryCode));
    }

    [Fact]
    public async Task CreateRole_alinea_code_y_name_con_las_columnas()
    {
        using var context = CreateContext();
        var validator = new CreateRoleValidator(context);

        var atLimit = await validator.ValidateAsync(new CreateRoleCommand(
            new string('A', DbFieldLengths.Roles.Code),
            new string('N', DbFieldLengths.Roles.Name),
            Description: null));
        AssertNoError(atLimit, nameof(CreateRoleCommand.Code));
        AssertNoError(atLimit, nameof(CreateRoleCommand.Name));

        var overLimit = await validator.ValidateAsync(new CreateRoleCommand(
            new string('A', DbFieldLengths.Roles.Code + 1),
            new string('N', DbFieldLengths.Roles.Name + 1),
            Description: null));
        AssertHasError(overLimit, nameof(CreateRoleCommand.Code));
        AssertHasError(overLimit, nameof(CreateRoleCommand.Name));
    }

    [Fact]
    public async Task Register_alinea_username_email_y_nombres_con_las_columnas()
    {
        using var context = CreateContext();
        var validator = new RegisterValidator(context);

        var atLimit = await validator.ValidateAsync(new RegisterCommand(
            Username: new string('u', DbFieldLengths.Users.Username),
            Email: $"{new string('e', 60)}@test.local",
            Password: "password123",
            FirstName: new string('F', DbFieldLengths.Users.FirstName),
            LastName: new string('L', DbFieldLengths.Users.LastName),
            RoleCode: "ADMIN"));
        AssertNoError(atLimit, nameof(RegisterCommand.Username));
        AssertNoError(atLimit, nameof(RegisterCommand.FirstName));
        AssertNoError(atLimit, nameof(RegisterCommand.LastName));

        var overLimit = await validator.ValidateAsync(new RegisterCommand(
            Username: new string('u', DbFieldLengths.Users.Username + 1),
            Email: $"{new string('e', DbFieldLengths.Users.Email)}@test.local",
            Password: "password123",
            FirstName: new string('F', DbFieldLengths.Users.FirstName + 1),
            LastName: new string('L', DbFieldLengths.Users.LastName + 1),
            RoleCode: "ADMIN"));
        AssertHasError(overLimit, nameof(RegisterCommand.Username));
        AssertHasError(overLimit, nameof(RegisterCommand.Email));
        AssertHasError(overLimit, nameof(RegisterCommand.FirstName));
        AssertHasError(overLimit, nameof(RegisterCommand.LastName));
    }

    [Fact]
    public async Task CreateAcquisition_alinea_purchase_order_e_invoice_number()
    {
        using var context = CreateContext();
        var validator = new CreateAcquisitionValidator(context);

        var overLimit = await validator.ValidateAsync(new CreateAcquisitionCommand(
            SupplierId: null,
            PurchaseOrder: new string('P', DbFieldLengths.Acquisitions.PurchaseOrder + 1),
            InvoiceNumber: new string('I', DbFieldLengths.Acquisitions.InvoiceNumber + 1),
            AcquiredAt: null,
            Subtotal: null,
            TaxAmount: null,
            TotalAmount: null,
            CurrencyCode: "MXN",
            WarrantyStart: null,
            WarrantyEnd: null,
            Notes: null,
            Assets: [new AcquisitionAssetInput(1, null)]));

        AssertHasError(overLimit, nameof(CreateAcquisitionCommand.PurchaseOrder));
        AssertHasError(overLimit, nameof(CreateAcquisitionCommand.InvoiceNumber));
    }

    [Fact]
    public void UploadDocument_alinea_file_name_con_la_columna()
    {
        var validator = new UploadDocumentValidator();

        var atLimit = validator.Validate(new UploadDocumentCommand(
            DocumentOwnerTypes.Incident,
            OwnerId: 1,
            Description: null,
            File: new FakeFormFile
            {
                FileName = new string('f', DbFieldLengths.Documents.FileName - 4) + ".pdf"
            }));
        AssertNoError(atLimit, nameof(UploadDocumentCommand.File));

        var overLimit = validator.Validate(new UploadDocumentCommand(
            DocumentOwnerTypes.Incident,
            OwnerId: 1,
            Description: null,
            File: new FakeFormFile
            {
                FileName = new string('f', DbFieldLengths.Documents.FileName + 1)
            }));
        Assert.Contains(overLimit.Errors,
            e => e.ErrorMessage.Contains("nombre del archivo", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CreateSupplier_alinea_campos_con_las_columnas()
    {
        using var context = CreateContext();
        var validator = new CreateSupplierValidator(context);

        var overLimit = await validator.ValidateAsync(new CreateSupplierCommand(
            Name: new string('N', DbFieldLengths.Suppliers.Name + 1),
            TaxId: new string('T', DbFieldLengths.Suppliers.TaxId + 1),
            ContactName: new string('C', DbFieldLengths.Suppliers.ContactName + 1),
            Email: null,
            Phone: new string('P', DbFieldLengths.Suppliers.Phone + 1),
            Address: null,
            Notes: null));

        AssertHasError(overLimit, nameof(CreateSupplierCommand.Name));
        AssertHasError(overLimit, nameof(CreateSupplierCommand.TaxId));
        AssertHasError(overLimit, nameof(CreateSupplierCommand.ContactName));
        AssertHasError(overLimit, nameof(CreateSupplierCommand.Phone));
    }

    [Fact]
    public async Task UpdateSupplier_alinea_campos_que_antes_no_tenian_maximo()
    {
        using var context = CreateContext();
        var validator = new UpdateSupplierValidator(context);

        var overLimit = await validator.ValidateAsync(new UpdateSupplierCommand(
            SupplierId: 1,
            ExpectedVersion: 1,
            Name: "Proveedor",
            TaxId: new string('T', DbFieldLengths.Suppliers.TaxId + 1),
            ContactName: new string('C', DbFieldLengths.Suppliers.ContactName + 1),
            Email: null,
            Phone: new string('P', DbFieldLengths.Suppliers.Phone + 1),
            Address: null,
            Notes: null));

        AssertHasError(overLimit, nameof(UpdateSupplierCommand.TaxId));
        AssertHasError(overLimit, nameof(UpdateSupplierCommand.ContactName));
        AssertHasError(overLimit, nameof(UpdateSupplierCommand.Phone));
    }

    [Fact]
    public async Task CreateAssetWarranty_alinea_warranty_number_con_la_columna()
    {
        using var context = CreateContext();
        var validator = new CreateAssetWarrantyValidator(context);

        var overLimit = await validator.ValidateAsync(new CreateAssetWarrantyCommand(
            AssetId: 1,
            SupplierId: null,
            WarrantyNumber: new string('W', DbFieldLengths.Warranties.WarrantyNumber + 1),
            StartsOn: null,
            EndsOn: null,
            Coverage: null,
            Terms: null));

        AssertHasError(overLimit, nameof(CreateAssetWarrantyCommand.WarrantyNumber));
    }
}
