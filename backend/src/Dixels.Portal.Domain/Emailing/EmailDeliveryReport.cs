namespace Dixels.Portal.Emailing;

/* What happened to one email sent through IEmailDeliveryProbe, plus the settings it was sent with. */
public class EmailDeliveryReport
{
    public bool Succeeded { get; set; }

    /* False when ExchangeOnline.Enabled is off: the email was only logged. */
    public bool SendingEnabled { get; set; }

    public string? AuthType { get; set; }

    public string? ServiceMailbox { get; set; }

    public string? Host { get; set; }

    public int? Port { get; set; }

    /* The server's reply, e.g. "2.0.0 OK <id> [Hostname=...]" on success or the rejection text on failure. */
    public string? ServerResponse { get; set; }

    public int? StatusCode { get; set; }

    public string? EnhancedStatusCode { get; set; }

    /* Whether a queued email failing this way would be retried. */
    public bool IsTransient { get; set; }

    public string? ErrorMessage { get; set; }

    /* The setting that is missing or wrong, when that is the cause. */
    public string? SettingName { get; set; }

    public long ElapsedMilliseconds { get; set; }
}
