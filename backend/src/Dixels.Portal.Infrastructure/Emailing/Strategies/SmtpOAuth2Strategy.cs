using System.Threading;
using System.Threading.Tasks;
using Dixels.Portal.Settings;
using MailKit.Net.Smtp;
using MailKit.Security;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Settings;

namespace Dixels.Portal.Emailing.Strategies;

/* Microsoft 365's supported way: the portal signs in to Entra ID as its app registration (client credentials), gets
 * an access token for Exchange Online, and presents it over SMTP with XOAUTH2 on behalf of the service mailbox.
 * The app needs the SMTP.SendAsApp permission and access to that mailbox (docs/DEPLOYMENT.md). */
[ExposeServices(typeof(ISmtpAuthStrategy))]
public class SmtpOAuth2Strategy : ISmtpAuthStrategy
{
    private readonly ISettingProvider _settings;
    private readonly ExchangeTokenProvider _tokens;

    public SmtpOAuth2Strategy(ISettingProvider settings, ExchangeTokenProvider tokens)
    {
        _settings = settings;
        _tokens = tokens;
    }

    public ExchangeAuthType AuthType => ExchangeAuthType.OAuth2;

    public async Task AuthenticateAsync(SmtpClient client, CancellationToken cancellationToken = default)
    {
        var tenantId = await _settings.GetRequiredAsync(ExchangeOnlineSettingNames.TenantId);
        var clientId = await _settings.GetRequiredAsync(ExchangeOnlineSettingNames.ClientId);
        var clientSecret = await _settings.GetRequiredAsync(ExchangeOnlineSettingNames.ClientSecret);
        var mailbox = await _settings.GetRequiredAsync(ExchangeOnlineSettingNames.ServiceMailbox);

        var accessToken = await _tokens.GetAccessTokenAsync(tenantId, clientId, clientSecret, cancellationToken);
        await client.AuthenticateAsync(new SaslMechanismOAuth2(mailbox, accessToken), cancellationToken);
    }
}
