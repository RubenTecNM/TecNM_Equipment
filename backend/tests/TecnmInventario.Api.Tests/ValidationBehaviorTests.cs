using FluentValidation;
using Mediator;
using TecnmInventario.Api.Common.Behaviors;
using ValidationException = TecnmInventario.Api.Common.Errors.ValidationException;

namespace TecnmInventario.Api.Tests;

/// <summary>
/// 2.8: el ValidationBehavior ejecuta los validadores en serie y acumula
/// todos los fallos en una sola ValidationException (mismo formato).
/// </summary>
public class ValidationBehaviorTests
{
    private sealed record TestCommand(string Value) : ICommand<string>;

    private sealed class AlwaysFailsValidator : AbstractValidator<TestCommand>
    {
        public AlwaysFailsValidator(string property, string message)
        {
            RuleFor(_ => _.Value)
                .Must(_ => false)
                .WithMessage(message)
                .WithName(property);
        }
    }

    [Fact]
    public async Task Acumula_los_fallos_de_todos_los_validadores()
    {
        var behavior = new ValidationBehavior<TestCommand, string>(
            new IValidator<TestCommand>[]
            {
                new AlwaysFailsValidator("Value", "primer fallo"),
                new AlwaysFailsValidator("Value", "segundo fallo")
            });

        var exception = await Assert.ThrowsAsync<ValidationException>(() => behavior.Handle(
            new TestCommand("x"),
            (_, _) => ValueTask.FromResult("ok"),
            CancellationToken.None).AsTask());

        Assert.True(exception.Errors["Value"].Length >= 2);
        Assert.Contains("primer fallo", exception.Errors["Value"]);
        Assert.Contains("segundo fallo", exception.Errors["Value"]);
    }

    [Fact]
    public async Task Sin_fallos_continua_al_siguiente_behavior()
    {
        var behavior = new ValidationBehavior<TestCommand, string>(
            Array.Empty<IValidator<TestCommand>>());

        var result = await behavior.Handle(
            new TestCommand("x"),
            (_, _) => ValueTask.FromResult("ok"),
            CancellationToken.None);

        Assert.Equal("ok", result);
    }
}
