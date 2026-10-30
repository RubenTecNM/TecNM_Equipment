using FluentValidation;
using FluentValidation.Results;
using Mediator;
using ValidationException = TecnmInventario.Api.Common.Errors.ValidationException;

namespace TecnmInventario.Api.Common.Behaviors;

public class ValidationBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    private readonly IEnumerable<IValidator<TMessage>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TMessage>> validators)
    {
        _validators = validators;
    }

    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
        {
            return await next(message, cancellationToken);
        }

        var context = new ValidationContext<TMessage>(message);

        // En serie, no en paralelo: todos los validadores comparten el
        // DbContext scoped del request. Task.WhenAll lanzaba dos operaciones
        // simultáneas sobre el mismo contexto ("A second operation was started
        // on this context instance") en cuanto un validador consultaba la BD.
        // Se acumulan todos los fallos igual que antes.
        var failures = new List<ValidationFailure>();
        foreach (var validator in _validators)
        {
            var result = await validator.ValidateAsync(context, cancellationToken);
            failures.AddRange(result.Errors.Where(failure => failure is not null));
        }

        if (failures.Count != 0)
        {
            throw new ValidationException(failures);
        }

        return await next(message, cancellationToken);
    }
}
