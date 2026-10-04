namespace Dixels.Portal.Emailing;

/* Exchange Online's limits for one mailbox sending over SMTP: 30 messages a minute and 3 connections at a time.
 * Going over them gets 4xx rejections (e.g. 432 4.3.2), so the sender stays under them instead.
 * Change with Configure<ExchangeOnlineThrottleOptions>(...) in a module. */
public class ExchangeOnlineThrottleOptions
{
    public int MessagesPerMinute { get; set; } = 30;

    public int MaxConcurrentConnections { get; set; } = 3;
}
