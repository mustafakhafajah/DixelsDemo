using System.Threading.Tasks;

namespace Dixels.Portal.Cqrs;

/* Finds the one handler for a command and runs it. Callers depend on this, not on handler classes. */
public interface ICommandDispatcher
{
    Task<TResult> SendAsync<TResult>(ICommand<TResult> command);
}
