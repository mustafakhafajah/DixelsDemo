using System.ComponentModel.DataAnnotations;

namespace Dixels.Portal.Bookings;

/* What the person cancelling wants to tell everyone affected, like the note in a Teams cancellation. Both optional:
 * Subject replaces the email's usual subject, Message is shown above the list of cancelled bookings. The owner and
 * everyone invited get the same words. */
public class CancellationMessageDto
{
    public const int MaxSubjectLength = 200;
    public const int MaxMessageLength = 2000;

    [StringLength(MaxSubjectLength)]
    public string? Subject { get; set; }

    [StringLength(MaxMessageLength)]
    public string? Message { get; set; }
}
