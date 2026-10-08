using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Dixels.Portal;

/* The web tests run the real Dixels.Portal.Web host, and ABP's test base adds that project's git-ignored
 * appsettings.secrets.json to it. On the server that file holds the live settings (the live database, the
 * Microsoft 365 mailbox), so a test could send real email or depend on how this machine is set up. This builder
 * passes everything through and, as the very last step before the host is built, drops that one file. */
internal sealed class WithoutMachineSecretsHostBuilder : IHostBuilder
{
    public const string SecretsFileName = "appsettings.secrets.json";

    private readonly IHostBuilder _inner;

    public WithoutMachineSecretsHostBuilder(IHostBuilder inner)
    {
        _inner = inner;
    }

    public IDictionary<object, object> Properties => _inner.Properties;

    public IHost Build()
    {
        /* Registered last, so it runs after every other source, ABP's secrets file included, has been added. */
        _inner.ConfigureAppConfiguration((_, config) =>
        {
            foreach (var secrets in config.Sources.OfType<JsonConfigurationSource>().Where(IsSecretsFile).ToList())
                config.Sources.Remove(secrets);
        });
        return _inner.Build();
    }

    public static bool IsSecretsFile(FileConfigurationSource source)
        => string.Equals(Path.GetFileName(source.Path), SecretsFileName, StringComparison.OrdinalIgnoreCase);

    public IHostBuilder ConfigureAppConfiguration(Action<HostBuilderContext, IConfigurationBuilder> configureDelegate)
    {
        _inner.ConfigureAppConfiguration(configureDelegate);
        return this;
    }

    public IHostBuilder ConfigureContainer<TContainerBuilder>(Action<HostBuilderContext, TContainerBuilder> configureDelegate)
    {
        _inner.ConfigureContainer(configureDelegate);
        return this;
    }

    public IHostBuilder ConfigureHostConfiguration(Action<IConfigurationBuilder> configureDelegate)
    {
        _inner.ConfigureHostConfiguration(configureDelegate);
        return this;
    }

    public IHostBuilder ConfigureServices(Action<HostBuilderContext, IServiceCollection> configureDelegate)
    {
        _inner.ConfigureServices(configureDelegate);
        return this;
    }

    public IHostBuilder UseServiceProviderFactory<TContainerBuilder>(IServiceProviderFactory<TContainerBuilder> factory)
        where TContainerBuilder : notnull
    {
        _inner.UseServiceProviderFactory(factory);
        return this;
    }

    public IHostBuilder UseServiceProviderFactory<TContainerBuilder>(Func<HostBuilderContext, IServiceProviderFactory<TContainerBuilder>> factory)
        where TContainerBuilder : notnull
    {
        _inner.UseServiceProviderFactory(factory);
        return this;
    }
}
