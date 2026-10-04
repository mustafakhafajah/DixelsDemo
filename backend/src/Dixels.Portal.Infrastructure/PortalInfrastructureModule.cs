using Volo.Abp.MailKit;
using Volo.Abp.Modularity;

namespace Dixels.Portal;

/* Talking to outside systems, kept out of Domain so it stays free of their libraries. Today: sending email through
 * Microsoft 365 / Exchange Online with MailKit and MSAL (see Emailing/ExchangeSmtpEmailSender). Only the Web host
 * depends on this module; the DbMigrator sends no email. */
[DependsOn(
    typeof(PortalDomainModule),
    typeof(AbpMailKitModule)
)]
public class PortalInfrastructureModule : AbpModule
{
}
