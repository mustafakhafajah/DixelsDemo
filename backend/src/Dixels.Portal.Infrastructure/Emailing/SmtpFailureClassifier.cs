using System;
using System.IO;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Identity.Client;

namespace Dixels.Portal.Emailing;

/* Turns what MailKit and MSAL throw into an EmailDeliveryException that says whether trying again could help.
 * SMTP's own rule decides: a 4xx reply is temporary (e.g. 432 4.3.2 too many connections, 451 4.7.0 throttled),
 * a 5xx reply is final (e.g. 535 5.7.139 sign-in refused, 554 5.2.0 quota). Dropped connections and timeouts are
 * temporary; mistakes in settings or in the message itself are final. */
public static partial class SmtpFailureClassifier
{
    public static EmailDeliveryException Classify(Exception exception)
    {
        switch (exception)
        {
            case EmailDeliveryException alreadyClassified:
                return alreadyClassified;

            case SmtpCommandException command:
            {
                var status = (int)command.StatusCode;
                return new EmailDeliveryException(
                    $"The mail server refused the email: {(int)command.StatusCode} {command.Message}",
                    isTransient: status is >= 400 and < 500,
                    smtpStatusCode: status,
                    enhancedStatusCode: EnhancedStatusCode(command.Message),
                    serverResponse: command.Message,
                    innerException: command);
            }

            /* MailKit gives only the server's text for a refused sign-in; the class digit of the enhanced code tells
             * temporary from final, and a refused sign-in without one is final. */
            case AuthenticationException auth:
            {
                var enhanced = EnhancedStatusCode(auth.Message);
                return new EmailDeliveryException(
                    $"The mail server refused the sign-in: {auth.Message}",
                    isTransient: enhanced?.StartsWith('4') == true,
                    enhancedStatusCode: enhanced,
                    serverResponse: auth.Message,
                    innerException: auth);
            }

            case MsalServiceException entra:
                return new EmailDeliveryException(
                    $"Microsoft Entra ID refused the app's sign-in ({entra.ErrorCode}): {entra.Message}",
                    isTransient: entra.IsRetryable,
                    serverResponse: entra.Message,
                    innerException: entra);

            case MsalException msal:
                return new EmailDeliveryException(
                    $"Could not get a token from Microsoft Entra ID ({msal.ErrorCode}): {msal.Message}",
                    isTransient: msal.IsRetryable,
                    innerException: msal);

            case SmtpProtocolException or ServiceNotConnectedException or ServiceNotAuthenticatedException
                or SocketException or IOException or TimeoutException or OperationCanceledException:
                return new EmailDeliveryException(
                    $"The connection to the mail server failed: {exception.Message}",
                    isTransient: true,
                    innerException: exception);

            default:
                return new EmailDeliveryException(
                    $"The email could not be sent: {exception.Message}",
                    isTransient: false,
                    innerException: exception);
        }
    }

    /* "5.7.139" in "5.7.139 Authentication unsuccessful, ...". */
    public static string? EnhancedStatusCode(string? text)
    {
        var match = EnhancedStatusCodePattern().Match(text ?? "");
        return match.Success ? match.Value : null;
    }

    [GeneratedRegex(@"\b[245]\.\d{1,3}\.\d{1,3}\b")]
    private static partial Regex EnhancedStatusCodePattern();
}
