using TecnmInventario.Api.Features.Documents.DeleteDocument;
using TecnmInventario.Api.Features.Locations.DeleteLocation;
using TecnmInventario.Api.Features.Procurement.Acquisitions.DeleteAcquisition;
using TecnmInventario.Api.Features.Procurement.Suppliers.DeleteSupplier;
using TecnmInventario.Api.Features.Procurement.Suppliers.UpdateSupplier;
using TecnmInventario.Api.Features.Procurement.Warranties.DeleteAssetWarranty;

namespace TecnmInventario.Api.Tests;

public class ExpectedVersionValidatorTests
{
    public static TheoryData<string> DeleteValidators => new()
    {
        nameof(DeleteLocationValidator),
        nameof(DeleteDocumentValidator),
        nameof(DeleteSupplierValidator),
        nameof(DeleteAcquisitionValidator),
        nameof(DeleteAssetWarrantyValidator)
    };

    [Theory]
    [MemberData(nameof(DeleteValidators))]
    public void Delete_con_ExpectedVersion_cero_es_invalido(string validatorName)
    {
        var result = validatorName switch
        {
            nameof(DeleteLocationValidator) =>
                new DeleteLocationValidator().Validate(new DeleteLocationCommand(1, 0)),
            nameof(DeleteDocumentValidator) =>
                new DeleteDocumentValidator().Validate(new DeleteDocumentCommand(1, 0)),
            nameof(DeleteSupplierValidator) =>
                new DeleteSupplierValidator().Validate(new DeleteSupplierCommand(1, 0)),
            nameof(DeleteAcquisitionValidator) =>
                new DeleteAcquisitionValidator().Validate(new DeleteAcquisitionCommand(1, 0)),
            nameof(DeleteAssetWarrantyValidator) =>
                new DeleteAssetWarrantyValidator().Validate(new DeleteAssetWarrantyCommand(1, 0)),
            _ => throw new ArgumentOutOfRangeException(nameof(validatorName))
        };

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "ExpectedVersion");
    }

    [Fact]
    public async Task UpdateSupplier_con_ExpectedVersion_cero_es_invalido()
    {
        using var context = TestDb.Create();
        var validator = new UpdateSupplierValidator(context);

        var result = await validator.ValidateAsync(new UpdateSupplierCommand(
            SupplierId: 1,
            ExpectedVersion: 0,
            Name: "Proveedor de prueba",
            TaxId: null,
            ContactName: null,
            Email: null,
            Phone: null,
            Address: null,
            Notes: null));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors,
            e => e.PropertyName == nameof(UpdateSupplierCommand.ExpectedVersion));
    }
}
