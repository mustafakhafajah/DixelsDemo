using System.IO;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Emailing;

namespace Dixels.Portal.Bookings;

/* A booking email that carries a calendar invitation (or cancellation). Queued like ABP's own queued emails, so it
 * is only sent once the booking change is saved, and retried if the mail server is down. */
[BackgroundJobName("BookingCalendarEmail")]
public class BookingCalendarEmailArgs
{
    public string To { get; set; } = null!;
    public string Subject { get; set; } = null!;
    public string Html { get; set; } = null!;
    public string Text { get; set; } = null!;
    /* The iCalendar text (BookingCalendar.Build). */
    public string Calendar { get; set; } = null!;
    /* BookingCalendar.Request or BookingCalendar.Cancel. */
    public string Method { get; set; } = null!;
}

/* Builds the message the way Outlook and Gmail send invitations: one multipart/alternative with a plain-text part,
 * an HTML part and a text/calendar part (method=REQUEST or CANCEL), plus the same calendar as an invite.ics
 * attachment for mail programs that only look at attachments. ABP's sender fills in the From address. */
public class BookingCalendarEmailJob : AsyncBackgroundJob<BookingCalendarEmailArgs>, ITransientDependency
{
    private readonly IEmailSender _emailSender;

    public BookingCalendarEmailJob(IEmailSender emailSender)
    {
        _emailSender = emailSender;
    }

    public override async Task ExecuteAsync(BookingCalendarEmailArgs args)
    {
        using var mail = Build(args);
        await _emailSender.SendAsync(mail);
    }

    public static MailMessage Build(BookingCalendarEmailArgs args)
    {
        var mail = new MailMessage
        {
            Subject = args.Subject,
            SubjectEncoding = Encoding.UTF8,
            BodyEncoding = Encoding.UTF8,
            HeadersEncoding = Encoding.UTF8,
        };
        mail.To.Add(args.To);
        mail.AlternateViews.Add(View(args.Text, "text/plain", TransferEncoding.QuotedPrintable));
        mail.AlternateViews.Add(View(args.Html, "text/html", TransferEncoding.QuotedPrintable));
        var calendar = View(args.Calendar, "text/calendar", TransferEncoding.Base64);
        calendar.ContentType.Parameters["method"] = args.Method;
        mail.AlternateViews.Add(calendar);

        var file = new Attachment(new MemoryStream(Encoding.UTF8.GetBytes(args.Calendar)), "invite.ics", "application/ics");
        file.ContentDisposition!.FileName = "invite.ics";
        mail.Attachments.Add(file);
        return mail;
    }

    /* The calendar goes as base64, as Exchange sends it, so its CRLF line ends and folding arrive untouched. */
    private static AlternateView View(string content, string mediaType, TransferEncoding encoding)
    {
        var view = AlternateView.CreateAlternateViewFromString(content, new ContentType(mediaType) { CharSet = "utf-8" });
        view.TransferEncoding = encoding;
        return view;
    }
}
