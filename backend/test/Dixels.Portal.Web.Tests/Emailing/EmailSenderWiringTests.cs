using Shouldly;
using Volo.Abp.Emailing;
using Volo.Abp.MailKit;
using Xunit;

namespace Dixels.Portal.Emailing;

/* The real host sends email with ABP's own MailKit sender (AbpMailKitModule in PortalDomainModule), configured by
 * ABP's Abp.Mailing.Smtp.* settings; there is no custom sender in between. */
public class EmailSenderWiringTests : PortalWebTestBase
{
    [Fact]
    public void Email_sender_is_ABPs_MailKit_sender() =>
        GetRequiredService<IEmailSender>().ShouldBeOfType<MailKitSmtpEmailSender>();
}
