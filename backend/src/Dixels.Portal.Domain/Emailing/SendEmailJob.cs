using System.IO;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Emailing;

namespace Dixels.Portal.Emailing;

/* Sends one queued email. Every queued email goes through here, including ABP's own (password reset and the like),
 * because the email sender's QueueAsync enqueues this job instead of ABP's.
 *
 * Retries: when this throws, ABP keeps the job and tries again later with growing waits (by default 1 minute, then
 * doubling, giving up after 2 days). That is right for transient failures (SMTP 4xx such as 432 4.3.2 "too many
 * connections", or the network). A permanent failure (SMTP 5xx such as 535 5.7.139, or a missing setting) would only
 * fail the same way for 2 days, so it is logged as an error and the job ends: ABP has no "failed, don't retry" state,
 * so ending the job is how it stops. */
public class SendEmailJob : AsyncBackgroundJob<SendEmailJobArgs>, ITransientDependency
{
    private readonly IEmailSender _emailSender;

    public SendEmailJob(IEmailSender emailSender)
    {
        _emailSender = emailSender;
    }

    public override async Task ExecuteAsync(SendEmailJobArgs args)
    {
        using var mail = BuildMailMessage(args);
        try
        {
            await _emailSender.SendAsync(mail);
        }
        catch (EmailDeliveryException ex) when (!ex.IsTransient)
        {
            Logger.LogError(ex,
                "Email to {To} (subject \"{Subject}\") failed for good and will not be retried. SMTP {StatusCode} {EnhancedStatusCode}: {ServerResponse}",
                args.To, args.Subject, ex.SmtpStatusCode, ex.EnhancedStatusCode, ex.ServerResponse ?? ex.Message);
        }
    }

    public static MailMessage BuildMailMessage(SendEmailJobArgs args)
    {
        var mail = new MailMessage { Subject = args.Subject, Body = args.Body, IsBodyHtml = args.IsBodyHtml };
        if (!string.IsNullOrWhiteSpace(args.From)) mail.From = new MailAddress(args.From);
        mail.To.Add(args.To);
        foreach (var cc in args.Cc ?? []) mail.CC.Add(cc);
        if (!string.IsNullOrWhiteSpace(args.ReplyTo)) mail.ReplyToList.Add(args.ReplyTo);
        foreach (var attachment in args.Attachments ?? [])
        {
            if (attachment.File != null) mail.Attachments.Add(new Attachment(new MemoryStream(attachment.File), attachment.Name));
        }
        return mail;
    }
}
