using System.Threading;
using System.Threading.Tasks;
using Dixels.Portal.Settings;
using MailKit.Net.Smtp;
using NSubstitute;
using Shouldly;
using Volo.Abp.Settings;
using Xunit;

namespace Dixels.Portal.Emailing;

public class SmtpAuthStrategyResolverTests
{
    private readonly FakeStrategy _oauth2 = new(ExchangeAuthType.OAuth2);
    private readonly FakeStrategy _basic = new(ExchangeAuthType.Basic);

    [Theory]
    [InlineData("OAuth2", ExchangeAuthType.OAuth2)]
    [InlineData("oauth2", ExchangeAuthType.OAuth2)]
    [InlineData("Basic", ExchangeAuthType.Basic)]
    public async Task Picks_the_strategy_named_by_the_setting(string setting, ExchangeAuthType expected)
    {
        var strategy = await Resolver(setting).ResolveAsync();

        strategy.AuthType.ShouldBe(expected);
    }

    [Theory]
    [InlineData("Kerberos")]
    [InlineData("1")]
    [InlineData("")]
    public async Task Rejects_anything_else_naming_the_setting(string setting)
    {
        var failure = await Should.ThrowAsync<EmailDeliveryException>(() => Resolver(setting).ResolveAsync());

        failure.IsTransient.ShouldBeFalse();
        failure.SettingName.ShouldBe(ExchangeOnlineSettingNames.AuthType);
    }

    private SmtpAuthStrategyResolver Resolver(string authType)
    {
        var settings = Substitute.For<ISettingProvider>();
        settings.GetOrNullAsync(ExchangeOnlineSettingNames.AuthType).Returns(authType);
        return new SmtpAuthStrategyResolver([_oauth2, _basic], settings);
    }

    private sealed class FakeStrategy(ExchangeAuthType authType) : ISmtpAuthStrategy
    {
        public ExchangeAuthType AuthType => authType;

        public Task AuthenticateAsync(SmtpClient client, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
