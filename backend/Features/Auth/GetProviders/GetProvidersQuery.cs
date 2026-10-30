using Mediator;

namespace TecnmInventario.Api.Features.Auth.GetProviders;

/// <summary>Proveedores federados configurados, para mostrar sus botones en el login.</summary>
public sealed record GetProvidersQuery : IQuery<GetProvidersResponse>;
