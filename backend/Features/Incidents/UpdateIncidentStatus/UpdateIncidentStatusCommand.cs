using Mediator;

namespace TecnmInventario.Api.Features.Incidents.UpdateIncidentStatus;

public sealed record UpdateIncidentStatusCommand(
    long IncidentId,
    int ExpectedVersion,
    string NewStatusCode,
    string? Reason) : ICommand<UpdateIncidentStatusResponse>;
