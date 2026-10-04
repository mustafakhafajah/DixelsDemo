using System.ComponentModel.DataAnnotations;

namespace Dixels.Portal.Emailing;

public class SendTestEmailInput
{
    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string To { get; set; } = default!;

    /* Empty: a standard test subject. */
    [StringLength(200)]
    public string? Subject { get; set; }

    /* Sets the Reply-To header, to check that replies go where they should. */
    [EmailAddress]
    [StringLength(256)]
    public string? ReplyTo { get; set; }
}
