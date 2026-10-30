using Mediator;

namespace TecnmInventario.Api.Features.Procurement.Acquisitions.DeleteAcquisition;

public sealed record DeleteAcquisitionCommand(long AcquisitionId, int ExpectedVersion)
    : ICommand<DeleteAcquisitionResponse>;
