using System.Net.Mail;
using System.Threading.Tasks;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Volo.Abp.Emailing;
using Xunit;

namespace Dixels.Portal.Emailing;

/* ABP retries a background job only when it throws, so: throw for failures worth retrying, finish quietly for the rest. */
public class SendEmailJobTests
{
    private readonly IEmailSender _sender = Substitute.For<IEmailSender>();

    [Fact]
    public async Task Sends_the_email_with_reply_to_and_copies()
    {
        MailMessage? sent = null;
        _sender.SendAsync(Arg.Do<MailMessage>(m => sent = m), Arg.Any<bool>()).Returns(Task.CompletedTask);

        await new SendEmailJob(_sender).ExecuteAsync(Args());

        sent.ShouldNotBeNull();
        sent.To.ToString().ShouldBe("someone@example.com");
        sent.CC.ToString().ShouldBe("copy@example.com");
        sent.ReplyToList.ToString().ShouldBe("frontdesk@dixels.io");
        sent.From.ShouldBeNull(); // filled in with the service mailbox by the sender
    }

    [Fact]
    public async Task A_transient_failure_is_thrown_so_ABP_retries()
    {
        _sender.SendAsync(Arg.Any<MailMessage>(), Arg.Any<bool>())
            .ThrowsAsync(new EmailDeliveryException("4.3.2 too many connections", isTransient: true, smtpStatusCode: 432));

        await Should.ThrowAsync<EmailDeliveryException>(() => new SendEmailJob(_sender).ExecuteAsync(Args()));
    }

    [Fact]
    public async Task A_permanent_failure_ends_the_job_without_a_retry()
    {
        _sender.SendAsync(Arg.Any<MailMessage>(), Arg.Any<bool>())
            .ThrowsAsync(new EmailDeliveryException("5.7.139 Authentication unsuccessful", isTransient: false, smtpStatusCode: 535));

        await Should.NotThrowAsync(() => new SendEmailJob(_sender).ExecuteAsync(Args()));
    }

    private static SendEmailJobArgs Args() => new()
    {
        To = "someone@example.com",
        Cc = ["copy@example.com"],
        ReplyTo = "frontdesk@dixels.io",
        Subject = "Your booking",
        Body = "<p>Booked.</p>"
    };
}
