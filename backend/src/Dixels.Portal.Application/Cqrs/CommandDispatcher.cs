using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.DependencyInjection;

namespace Dixels.Portal.Cqrs;

public class CommandDispatcher : ICommandDispatcher, ITransientDependency
{
    /* One small typed wrapper per command type, built once, so dispatch needs no reflection after the first call. */
    private static readonly ConcurrentDictionary<Type, object> Wrappers = new();

    private readonly IServiceProvider _serviceProvider;

    public CommandDispatcher(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public Task<TResult> SendAsync<TResult>(ICommand<TResult> command)
    {
        var wrapper = (CommandWrapper<TResult>)Wrappers.GetOrAdd(command.GetType(), type =>
            Activator.CreateInstance(typeof(CommandWrapper<,>).MakeGenericType(type, typeof(TResult)))!);
        return wrapper.HandleAsync(command, _serviceProvider);
    }

    private abstract class CommandWrapper<TResult>
    {
        public abstract Task<TResult> HandleAsync(ICommand<TResult> command, IServiceProvider serviceProvider);
    }

    private sealed class CommandWrapper<TCommand, TResult> : CommandWrapper<TResult> where TCommand : ICommand<TResult>
    {
        public override Task<TResult> HandleAsync(ICommand<TResult> command, IServiceProvider serviceProvider)
            => serviceProvider.GetRequiredService<ICommandHandler<TCommand, TResult>>().HandleAsync((TCommand)command);
    }
}
