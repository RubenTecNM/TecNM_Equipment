using Mediator;

namespace TecnmInventario.Api.Features.Incidents.ResolveIncident;

public sealed record ResolveIncidentCommand(
    long IncidentId,
    int ExpectedVersion,
    string Solution,
    string? Diagnosis) : ICommand<ResolveIncidentResponse>;
