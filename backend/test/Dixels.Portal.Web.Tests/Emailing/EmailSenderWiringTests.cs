using System.Net.Mail;
using System.Threading.Tasks;
using Dixels.Portal.Emailing;
using Shouldly;
using Volo.Abp.Emailing;
using Xunit;

namespace Dixels.Portal.Emailing;

/* The real host: whatever asks ABP for an email sender (ABP's account pages included) gets the Exchange Online one,
 * and with no settings it points at Exchange Online but sends nothing. */
public class EmailSenderWiringTests : PortalWebTestBase
{
    [Fact]
    public void Email_sender_is_the_exchange_online_sender()
    {
        GetRequiredService<IEmailSender>().ShouldBeOfType<ExchangeSmtpEmailSender>();
        GetRequiredService<IEmailDeliveryProbe>().ShouldBeOfType<ExchangeSmtpEmailSender>();
    }

    [Fact]
    public async Task Without_settings_nothing_is_sent_and_exchange_online_is_the_default()
    {
        using var mail = new MailMessage { Subject = "Test", Body = "Test" };
        mail.To.Add("someone@example.com");

        var report = await GetRequiredService<IEmailDeliveryProbe>().SendAndReportAsync(mail);

        report.SendingEnabled.ShouldBeFalse();
        report.Succeeded.ShouldBeFalse();
        report.Host.ShouldBe("smtp.office365.com");
        report.Port.ShouldBe(587);
        report.AuthType.ShouldBe(nameof(ExchangeAuthType.OAuth2));
    }
}
