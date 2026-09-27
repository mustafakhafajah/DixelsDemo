using System;
using System.Linq;
using Dixels.Portal.Cqrs;
using Shouldly;
using Xunit;

namespace Dixels.Portal.EntityFrameworkCore.Cqrs;

/* A command or query without a registered handler only fails when someone calls it. This catches it
 * at test time instead: every request type in the Application assembly must resolve exactly one handler. */
[Collection(PortalTestConsts.CollectionDefinitionName)]
public class HandlerRegistrationTests : PortalEntityFrameworkCoreTestBase
{
    [Fact]
    public void Every_command_and_query_has_a_handler()
    {
        var requests = typeof(PortalApplicationModule).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .SelectMany(t => t.GetInterfaces()
                .Where(i => i.IsGenericType &&
                            (i.GetGenericTypeDefinition() == typeof(ICommand<>) || i.GetGenericTypeDefinition() == typeof(IQuery<>)))
                .Select(i => (Request: t, Contract: i)))
            .ToList();

        requests.ShouldNotBeEmpty();
        foreach (var (request, contract) in requests)
        {
            var handlerOpen = contract.GetGenericTypeDefinition() == typeof(ICommand<>) ? typeof(ICommandHandler<,>) : typeof(IQueryHandler<,>);
            var handlerType = handlerOpen.MakeGenericType(request, contract.GetGenericArguments()[0]);
            ServiceProvider.GetService(handlerType).ShouldNotBeNull($"{request.Name} has no registered {handlerOpen.Name}");
        }
    }
}
