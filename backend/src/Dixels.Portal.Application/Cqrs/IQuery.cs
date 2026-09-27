namespace Dixels.Portal.Cqrs;

/* A request that only reads. A query handler must never change state. */
public interface IQuery<TResult>
{
}
