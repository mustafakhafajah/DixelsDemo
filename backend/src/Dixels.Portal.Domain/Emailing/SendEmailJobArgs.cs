using System;
using System.Collections.Generic;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Emailing;
using Volo.Abp.MultiTenancy;

namespace Dixels.Portal.Emailing;

/* One queued email. Stored as JSON in the background-job table until it is sent, so attachments should stay small.
 * IMultiTenant: ABP runs the job as this tenant, so it is sent with that tenant's settings. */
[BackgroundJobName("ExchangeEmailSenderJob")]
public class SendEmailJobArgs : IMultiTenant
{
    public Guid? TenantId { get; set; }

    /* Empty: sent from the service mailbox. */
    public string? From { get; set; }

    public string To { get; set; } = default!;

    public List<string>? Cc { get; set; }

    /* Where replies go, when that is not the sender (e.g. the person a notification is about). */
    public string? ReplyTo { get; set; }

    public string? Subject { get; set; }

    public string? Body { get; set; }

    public bool IsBodyHtml { get; set; } = true;

    public List<EmailAttachment>? Attachments { get; set; }
}
