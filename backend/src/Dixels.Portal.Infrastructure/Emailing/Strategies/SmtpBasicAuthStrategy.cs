using System.Threading;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Emailing;
using Volo.Abp.Settings;

namespace Dixels.Portal.Emailing.Strategies;

/* User name and password, from ABP's own mail settings (Abp.Mailing.Smtp.UserName / Password; the password is an
 * encrypted ABP setting, so it is entered on ABP's Settings > Emailing page rather than in a config file).
 * No user name: no sign-in at all, for a relay that accepts mail without it, such as the local Mailpit.
 * The connection is already STARTTLS-secured by the sender when Abp.Mailing.Smtp.EnableSsl is on. */
[ExposeServices(typeof(ISmtpAuthStrategy))]
public class SmtpBasicAuthStrategy : ISmtpAuthStrategy
{
    private readonly ISettingProvider _settings;

    public SmtpBasicAuthStrategy(ISettingProvider settings)
    {
        _settings = settings;
    }

    public ExchangeAuthType AuthType => ExchangeAuthType.Basic;

    public async Task AuthenticateAsync(SmtpClient client, CancellationToken cancellationToken = default)
    {
        var userName = await _settings.GetOrNullAsync(EmailSettingNames.Smtp.UserName);
        if (string.IsNullOrWhiteSpace(userName)) return;

        var password = await _settings.GetOrNullAsync(EmailSettingNames.Smtp.Password);
        await client.AuthenticateAsync(userName, password ?? "", cancellationToken);
    }
}
