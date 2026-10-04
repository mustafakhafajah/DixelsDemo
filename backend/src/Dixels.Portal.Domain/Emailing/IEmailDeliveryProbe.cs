using System.Net.Mail;
using System.Threading.Tasks;

namespace Dixels.Portal.Emailing;

/* Sends one email straight away (not queued) and reports exactly what happened, for the "send a test email" check.
 * Never throws for a delivery failure: the failure is in the report. */
public interface IEmailDeliveryProbe
{
    Task<EmailDeliveryReport> SendAndReportAsync(MailMessage mail);
}
