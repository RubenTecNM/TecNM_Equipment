namespace TecnmInventario.Api.Common.Behaviors;

/// <summary>
/// Marca un comando como escritura de negocio. <see cref="TransactionBehavior{TMessage,TResponse}"/>
/// le abre una transacción explícita y la confirma o revierte junto con las
/// filas de audit_log que el handler escriba dentro de ella.
/// </summary>
public interface ITransactionalCommand
{
}
