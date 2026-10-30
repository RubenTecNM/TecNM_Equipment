using Mediator;

namespace TecnmInventario.Api.Features.Procurement.Acquisitions.GetAcquisitionById;

public sealed record GetAcquisitionByIdQuery(long AcquisitionId) : IQuery<GetAcquisitionByIdResponse>;
