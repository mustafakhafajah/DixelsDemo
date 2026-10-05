using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Shouldly;
using Xunit;

namespace Dixels.Portal.Hosting;

/* The web tests must never see this machine's appsettings.secrets.json: on the server it holds the live database
 * and the Microsoft 365 mailbox, so a test could otherwise send real email. */
public class TestHostIsolationTests : PortalWebTestBase
{
    [Fact]
    public void The_test_host_does_not_read_the_machines_secrets_file()
    {
        var configuration = (IConfigurationRoot)GetRequiredService<IConfiguration>();

        configuration.Providers.OfType<JsonConfigurationProvider>()
            .Select(p => p.Source)
            .ShouldNotContain(source => WithoutMachineSecretsHostBuilder.IsSecretsFile(source));
    }

    [Fact]
    public void The_web_projects_own_settings_are_still_read()
    {
        var configuration = (IConfigurationRoot)GetRequiredService<IConfiguration>();

        configuration.Providers.OfType<JsonConfigurationProvider>()
            .ShouldContain(p => p.Source.Path == "appsettings.json");
    }
}
