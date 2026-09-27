using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.DependencyInjection;

namespace Dixels.Portal.Cqrs;

public class QueryDispatcher : IQueryDispatcher, ITransientDependency
{
    private static readonly ConcurrentDictionary<Type, object> Wrappers = new();

    private readonly IServiceProvider _serviceProvider;

    public QueryDispatcher(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public Task<TResult> QueryAsync<TResult>(IQuery<TResult> query)
    {
        var wrapper = (QueryWrapper<TResult>)Wrappers.GetOrAdd(query.GetType(), type =>
            Activator.CreateInstance(typeof(QueryWrapper<,>).MakeGenericType(type, typeof(TResult)))!);
        return wrapper.HandleAsync(query, _serviceProvider);
    }

    private abstract class QueryWrapper<TResult>
    {
        public abstract Task<TResult> HandleAsync(IQuery<TResult> query, IServiceProvider serviceProvider);
    }

    private sealed class QueryWrapper<TQuery, TResult> : QueryWrapper<TResult> where TQuery : IQuery<TResult>
    {
        public override Task<TResult> HandleAsync(IQuery<TResult> query, IServiceProvider serviceProvider)
            => serviceProvider.GetRequiredService<IQueryHandler<TQuery, TResult>>().HandleAsync((TQuery)query);
    }
}
