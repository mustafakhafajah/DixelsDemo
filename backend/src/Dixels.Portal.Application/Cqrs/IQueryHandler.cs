using System.Threading.Tasks;

namespace Dixels.Portal.Cqrs;

public interface IQueryHandler<in TQuery, TResult> where TQuery : IQuery<TResult>
{
    Task<TResult> HandleAsync(TQuery query);
}
