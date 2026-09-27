using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Dixels.Portal.Cqrs;

public static class CqrsServiceCollectionExtensions
{
    /* Registers every command and query handler in the assembly. ABP's naming convention can't expose
     * generic interfaces like ICommandHandler<,>, so handlers are found by scanning instead: adding a
     * use case means adding a handler class, nothing else to wire. */
    public static IServiceCollection AddCqrsHandlers(this IServiceCollection services, Assembly assembly)
    {
        var handlerInterfaces = new[] { typeof(ICommandHandler<,>), typeof(IQueryHandler<,>) };
        var registrations =
            from type in assembly.GetTypes()
            where type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false }
            from contract in type.GetInterfaces()
            where contract.IsGenericType && handlerInterfaces.Contains(contract.GetGenericTypeDefinition())
            select (contract, type);

        foreach (var (contract, type) in registrations)
        {
            services.AddTransient(contract, type);
        }
        return services;
    }
}
