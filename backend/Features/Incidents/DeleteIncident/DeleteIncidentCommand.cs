using Mediator;

namespace TecnmInventario.Api.Features.Incidents.DeleteIncident;

public sealed record DeleteIncidentCommand(
    long IncidentId,
    int ExpectedVersion) : ICommand<DeleteIncidentResponse>;
