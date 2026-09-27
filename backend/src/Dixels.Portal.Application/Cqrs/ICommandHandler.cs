using System.Threading.Tasks;

namespace Dixels.Portal.Cqrs;

public interface ICommandHandler<in TCommand, TResult> where TCommand : ICommand<TResult>
{
    Task<TResult> HandleAsync(TCommand command);
}
