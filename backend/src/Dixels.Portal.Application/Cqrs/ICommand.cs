namespace Dixels.Portal.Cqrs;

/* A request that changes state (create, update, cancel...). TResult is what the caller gets back;
 * use Unit when there is nothing to return. Handled by exactly one ICommandHandler. */
public interface ICommand<TResult>
{
}
