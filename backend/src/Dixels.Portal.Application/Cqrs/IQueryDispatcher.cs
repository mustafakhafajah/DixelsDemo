using System.Threading.Tasks;

namespace Dixels.Portal.Cqrs;

/* Finds the one handler for a query and runs it. */
public interface IQueryDispatcher
{
    Task<TResult> QueryAsync<TResult>(IQuery<TResult> query);
}
