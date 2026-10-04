namespace Dixels.Portal.Emailing;

/* The outcome of a test email. A failed send is still a 200 with Succeeded = false: the point is to see why. */
public class EmailTestResultDto
{
    public bool Succeeded { get; set; }

    /* False when ExchangeOnline.Enabled is off and the email was only logged. */
    public bool SendingEnabled { get; set; }

    public string? AuthType { get; set; }

    public string? ServiceMailbox { get; set; }

    public string? Host { get; set; }

    public int? Port { get; set; }

    /* The mail server's reply, e.g. "2.0.0 OK ..." or "5.7.139 Authentication unsuccessful ...". */
    public string? ServerResponse { get; set; }

    public int? StatusCode { get; set; }

    public string? EnhancedStatusCode { get; set; }

    /* Whether a queued email failing this way would be retried automatically. */
    public bool IsTransient { get; set; }

    public string? ErrorMessage { get; set; }

    /* The setting that is missing or wrong, when that is the cause. */
    public string? SettingName { get; set; }

    /* What to check, in plain words, for the common failures. */
    public string? Hint { get; set; }

    public long ElapsedMilliseconds { get; set; }
}
