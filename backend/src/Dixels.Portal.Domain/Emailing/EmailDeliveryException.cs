using System;
using Volo.Abp;

namespace Dixels.Portal.Emailing;

/* An email could not be delivered. Thrown by the email sender in place of the mail library's own exceptions, so the
 * rest of the code can decide what to do without knowing about MailKit or MSAL.
 * IsTransient: worth trying again later (SMTP 4xx, network trouble). Otherwise it will fail the same way every time
 * (SMTP 5xx such as 535 5.7.139, or a setting that is missing) and retrying only hides the problem. */
public class EmailDeliveryException : AbpException
{
    public bool IsTransient { get; }

    /* The SMTP reply code (e.g. 535), when the server sent one. */
    public int? SmtpStatusCode { get; }

    /* The enhanced status code from the reply text (e.g. "5.7.139"), when there was one. */
    public string? EnhancedStatusCode { get; }

    /* The server's reply as it was sent. */
    public string? ServerResponse { get; }

    /* The setting that is missing or wrong, when that is the cause. */
    public string? SettingName { get; }

    public EmailDeliveryException(
        string message,
        bool isTransient,
        int? smtpStatusCode = null,
        string? enhancedStatusCode = null,
        string? serverResponse = null,
        Exception? innerException = null,
        string? settingName = null)
        : base(message, innerException)
    {
        IsTransient = isTransient;
        SmtpStatusCode = smtpStatusCode;
        EnhancedStatusCode = enhancedStatusCode;
        ServerResponse = serverResponse;
        SettingName = settingName;
    }

    /* A setting the sender needs is not filled in. */
    public static EmailDeliveryException MissingSetting(string settingName) =>
        new($"The {settingName} setting is not set. Add it under \"Settings\" in appsettings.secrets.json (see docs/DEPLOYMENT.md).",
            isTransient: false, settingName: settingName);
}
