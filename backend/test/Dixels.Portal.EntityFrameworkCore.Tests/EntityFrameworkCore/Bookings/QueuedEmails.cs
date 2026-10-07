using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Bookings;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Emailing;

namespace Dixels.Portal.EntityFrameworkCore.Bookings;

/* An email waiting in the background-job table. Calendar and Method are set for the ones that carry a calendar
 * invitation or cancellation (BookingCalendarEmailJob); plain emails (ABP's queued email) have neither. */
internal record QueuedEmail(string To, string Subject, string Body, string? Calendar, string? Method);

/* Tests don't run background jobs, so the emails a change queued are read back from the job table. */
internal static class QueuedEmails
{
    public static async Task<List<QueuedEmail>> ReadAsync(IBackgroundJobRepository jobs, IBackgroundJobSerializer serializer)
    {
        var plain = BackgroundJobNameAttribute.GetName<BackgroundEmailSendingJobArgs>();
        var withCalendar = BackgroundJobNameAttribute.GetName<BookingCalendarEmailArgs>();
        var result = new List<QueuedEmail>();
        foreach (var job in (await jobs.GetListAsync()).OrderBy(j => j.CreationTime))
        {
            if (job.JobName == plain)
            {
                var a = (BackgroundEmailSendingJobArgs)serializer.Deserialize(job.JobArgs, typeof(BackgroundEmailSendingJobArgs));
                result.Add(new QueuedEmail(a.To, a.Subject ?? "", a.Body ?? "", null, null));
            }
            else if (job.JobName == withCalendar)
            {
                var a = (BookingCalendarEmailArgs)serializer.Deserialize(job.JobArgs, typeof(BookingCalendarEmailArgs));
                result.Add(new QueuedEmail(a.To, a.Subject, a.Html, a.Calendar, a.Method));
            }
        }
        return result;
    }
}
