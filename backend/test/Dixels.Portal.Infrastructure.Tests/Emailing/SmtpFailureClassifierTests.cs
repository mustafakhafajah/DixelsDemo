using System;
using System.IO;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Identity.Client;
using Shouldly;
using Xunit;

namespace Dixels.Portal.Emailing;

/* The classifier is pure, so these need no ABP base class. Transient = a queued email is retried; otherwise it is
 * logged and dropped. The replies are the ones Exchange Online actually sends. */
public class SmtpFailureClassifierTests
{
    [Fact]
    public void Too_many_connections_is_retried()
    {
        var failure = SmtpFailureClassifier.Classify(new SmtpCommandException(
            SmtpErrorCode.MessageNotAccepted, (SmtpStatusCode)432, "4.3.2 STOREDRV.ClientSubmit; sender thread limit exceeded"));

        failure.IsTransient.ShouldBeTrue();
        failure.SmtpStatusCode.ShouldBe(432);
        failure.EnhancedStatusCode.ShouldBe("4.3.2");
    }

    [Fact]
    public void Throttling_is_retried()
    {
        var failure = SmtpFailureClassifier.Classify(new SmtpCommandException(
            SmtpErrorCode.MessageNotAccepted, (SmtpStatusCode)451, "4.7.0 Temporary server error. Please try again later"));

        failure.IsTransient.ShouldBeTrue();
        failure.EnhancedStatusCode.ShouldBe("4.7.0");
    }

    [Fact]
    public void Quota_exceeded_is_not_retried()
    {
        var failure = SmtpFailureClassifier.Classify(new SmtpCommandException(
            SmtpErrorCode.MessageNotAccepted, (SmtpStatusCode)554, "5.2.0 STOREDRV.Submission.Exception:SubmissionQuotaExceededException"));

        failure.IsTransient.ShouldBeFalse();
        failure.SmtpStatusCode.ShouldBe(554);
        failure.EnhancedStatusCode.ShouldBe("5.2.0");
    }

    [Fact]
    public void Refused_sign_in_is_not_retried()
    {
        var failure = SmtpFailureClassifier.Classify(new AuthenticationException(
            "5.7.139 Authentication unsuccessful, SmtpClientAuthentication is disabled for the Mailbox."));

        failure.IsTransient.ShouldBeFalse();
        failure.EnhancedStatusCode.ShouldBe("5.7.139");
        failure.ServerResponse.ShouldNotBeNull().ShouldContain("SmtpClientAuthentication is disabled");
    }

    [Fact]
    public void Refused_sign_in_without_a_code_is_not_retried() =>
        SmtpFailureClassifier.Classify(new AuthenticationException("Authentication failed.")).IsTransient.ShouldBeFalse();

    [Fact]
    public void Dropped_connection_is_retried() =>
        SmtpFailureClassifier.Classify(new IOException("Connection reset")).IsTransient.ShouldBeTrue();

    [Fact]
    public void Wrong_client_secret_is_not_retried()
    {
        var failure = SmtpFailureClassifier.Classify(new MsalServiceException("invalid_client", "AADSTS7000215: Invalid client secret provided."));

        failure.IsTransient.ShouldBeFalse();
        failure.Message.ShouldContain("AADSTS7000215");
    }

    [Fact]
    public void Unknown_errors_are_not_retried() =>
        SmtpFailureClassifier.Classify(new FormatException("bad address")).IsTransient.ShouldBeFalse();

    [Fact]
    public void An_already_classified_failure_is_kept()
    {
        var original = EmailDeliveryException.MissingSetting("ExchangeOnline.TenantId");

        SmtpFailureClassifier.Classify(original).ShouldBeSameAs(original);
    }
}
