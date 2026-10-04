using System;
using System.Diagnostics;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using Dixels.Portal.Settings;
using MailKit.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Utils;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Emailing;
using Volo.Abp.Emailing.Smtp;
using Volo.Abp.MailKit;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Settings;
using SmtpClient = MailKit.Net.Smtp.SmtpClient;

namespace Dixels.Portal.Emailing;

/* The portal's IEmailSender: every module that sends email through ABP (ours, and ABP's own such as password reset)
 * ends up here. Built on ABP's MailKit sender, with these changes:
 *  - Off switch: until ExchangeOnline.Enabled is "true" emails are only logged (this replaces the old debug-only NullEmailSender).
 *  - Sign-in: the strategy named by ExchangeOnline.AuthType (OAuth2 token or Basic), over STARTTLS on 587.
 *  - From: always the service mailbox or an allowed alias; Exchange rejects anything else, so it is checked first.
 *  - Throttle: stays under Exchange Online's 30 messages a minute and 3 connections (ExchangeSendThrottle).
 *  - Failures: thrown as EmailDeliveryException, saying whether a retry could help (SmtpFailureClassifier).
 *  - Queueing: QueueAsync enqueues SendEmailJob, which retries only the failures worth retrying. */
[Dependency(ServiceLifetime.Transient, ReplaceServices = true)]
[ExposeServices(typeof(IEmailSender), typeof(IMailKitSmtpEmailSender), typeof(IEmailDeliveryProbe), typeof(ExchangeSmtpEmailSender))]
public class ExchangeSmtpEmailSender : MailKitSmtpEmailSender, IEmailDeliveryProbe
{
    private readonly ISettingProvider _settings;
    private readonly SmtpAuthStrategyResolver _authStrategies;
    private readonly ExchangeSendThrottle _throttle;
    private readonly ExchangeEmailJobManager _jobs;

    public ExchangeSmtpEmailSender(
        ICurrentTenant currentTenant,
        ISmtpEmailSenderConfiguration smtpConfiguration,
        IBackgroundJobManager backgroundJobManager,
        IOptions<AbpMailKitOptions> abpMailKitConfiguration,
        ISettingProvider settings,
        SmtpAuthStrategyResolver authStrategies,
        ExchangeSendThrottle throttle,
        ExchangeEmailJobManager jobs)
        : base(currentTenant, smtpConfiguration, backgroundJobManager, abpMailKitConfiguration)
    {
        _settings = settings;
        _authStrategies = authStrategies;
        _throttle = throttle;
        _jobs = jobs;
    }

    public override Task QueueAsync(string to, string subject, string body, bool isBodyHtml = true, AdditionalEmailSendingArgs? additionalEmailSendingArgs = null)
        => EnqueueAsync(null, to, subject, body, isBodyHtml, additionalEmailSendingArgs);

    public override Task QueueAsync(string from, string to, string subject, string body, bool isBodyHtml = true, AdditionalEmailSendingArgs? additionalEmailSendingArgs = null)
        => EnqueueAsync(from, to, subject, body, isBodyHtml, additionalEmailSendingArgs);

    public async Task<EmailDeliveryReport> SendAndReportAsync(MailMessage mail)
    {
        var report = new EmailDeliveryReport
        {
            SendingEnabled = await IsEnabledAsync(),
            ServiceMailbox = await _settings.GetOrNullAsync(ExchangeOnlineSettingNames.ServiceMailbox),
            AuthType = await _settings.GetOrNullAsync(ExchangeOnlineSettingNames.AuthType),
            Host = await SmtpConfiguration.GetHostAsync(),
            Port = await SmtpConfiguration.GetPortAsync()
        };
        var watch = Stopwatch.StartNew();
        try
        {
            await NormalizeMailAsync(mail);
            report.ServerResponse = await DeliverAsync(mail);
            report.Succeeded = report.SendingEnabled;
            if (!report.SendingEnabled) report.ErrorMessage = $"Sending is switched off ({ExchangeOnlineSettingNames.Enabled} is not \"true\"), so the email was only written to the log.";
        }
        catch (Exception ex)
        {
            var failure = SmtpFailureClassifier.Classify(ex);
            report.ErrorMessage = failure.Message;
            report.ServerResponse = failure.ServerResponse;
            report.StatusCode = failure.SmtpStatusCode;
            report.EnhancedStatusCode = failure.EnhancedStatusCode;
            report.IsTransient = failure.IsTransient;
            report.SettingName = failure.SettingName;
        }
        report.ElapsedMilliseconds = watch.ElapsedMilliseconds;
        return report;
    }

    protected override async Task SendEmailAsync(MailMessage mail)
    {
        await DeliverAsync(mail);
    }

    /* Fills in or checks From before ABP's own normalising (UTF-8 encodings). ABP would otherwise fill an empty From
     * with Abp.Mailing.DefaultFromAddress, which Exchange would reject unless it happened to be the service mailbox. */
    protected override async Task NormalizeMailAsync(MailMessage mail)
    {
        if (await IsEnabledAsync())
        {
            var mailbox = await _settings.GetRequiredAsync(ExchangeOnlineSettingNames.ServiceMailbox);
            if (mail.From == null || string.IsNullOrEmpty(mail.From.Address))
            {
                mail.From = new MailAddress(mailbox, await Configuration.GetDefaultFromDisplayNameAsync(), Encoding.UTF8);
            }
            else if (!SenderAddressPolicy.IsAllowed(mail.From.Address, mailbox, await _settings.GetOrNullAsync(ExchangeOnlineSettingNames.AllowedFromAddresses)))
            {
                throw new EmailDeliveryException(
                    $"{mail.From.Address} may not be used as the sender. Send from {mailbox}, or add the address to {ExchangeOnlineSettingNames.AllowedFromAddresses} once IT has allowed the mailbox to send as it.",
                    isTransient: false,
                    settingName: ExchangeOnlineSettingNames.AllowedFromAddresses);
            }
        }

        await base.NormalizeMailAsync(mail);
    }

    protected override async Task ConfigureClient(SmtpClient client)
    {
        await client.ConnectAsync(await SmtpConfiguration.GetHostAsync(), await SmtpConfiguration.GetPortAsync(), await GetSecureSocketOption());
        var strategy = await _authStrategies.ResolveAsync();
        await strategy.AuthenticateAsync(client);
    }

    /* Abp.Mailing.Smtp.EnableSsl means an encrypted connection: on port 465 that is TLS from the start, elsewhere
     * (Exchange Online's 587) STARTTLS, which must succeed. Off means plain, for the local Mailpit only. */
    protected override async Task<SecureSocketOptions> GetSecureSocketOption()
    {
        if (AbpMailKitOptions.SecureSocketOption.HasValue) return AbpMailKitOptions.SecureSocketOption.Value;
        if (!await SmtpConfiguration.GetEnableSslAsync()) return SecureSocketOptions.None;
        return await SmtpConfiguration.GetPortAsync() == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
    }

    /* Sends one normalised email and returns the server's reply (null when sending is off). */
    private async Task<string?> DeliverAsync(MailMessage mail)
    {
        if (!await IsEnabledAsync())
        {
            Logger.LogWarning("Email not sent because {Setting} is off. To: {To}; subject: \"{Subject}\".",
                ExchangeOnlineSettingNames.Enabled, mail.To.ToString(), mail.Subject);
            return null;
        }

        try
        {
            using var slot = await _throttle.AcquireAsync();
            using var client = await BuildClientAsync();
            var message = MimeMessage.CreateFromMailMessage(mail);
            message.MessageId = MimeUtils.GenerateMessageId();
            var response = await client.SendAsync(message);
            await client.DisconnectAsync(true);
            return response;
        }
        catch (Exception ex) when (ex is not EmailDeliveryException)
        {
            throw SmtpFailureClassifier.Classify(ex);
        }
    }

    private async Task EnqueueAsync(string? from, string to, string subject, string body, bool isBodyHtml, AdditionalEmailSendingArgs? additionalEmailSendingArgs)
    {
        await ValidateEmailAddressAsync(to);

        if (!_jobs.IsAvailable())
        {
            await SendAsync(BuildMailMessage(from, to, subject, body, isBodyHtml, additionalEmailSendingArgs));
            return;
        }

        await _jobs.EnqueueAsync(new SendEmailJobArgs
        {
            TenantId = CurrentTenant.Id,
            From = from,
            To = to,
            Cc = additionalEmailSendingArgs?.CC,
            Subject = subject,
            Body = body,
            IsBodyHtml = isBodyHtml,
            Attachments = additionalEmailSendingArgs?.Attachments
        });
    }

    private async Task<bool> IsEnabledAsync() =>
        string.Equals(await _settings.GetOrNullAsync(ExchangeOnlineSettingNames.Enabled), "true", StringComparison.OrdinalIgnoreCase);
}
