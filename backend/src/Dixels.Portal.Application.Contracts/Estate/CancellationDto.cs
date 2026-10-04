using System.ComponentModel.DataAnnotations;

namespace Dixels.Portal.Estate;

/* PATCH body that cancels something: { "lifecycle": "cancelled" }. Cancelled records are kept as history,
 * which is why cancelling is a change of state (PATCH) and not a DELETE. */
public class CancellationDto
{
    /* The resource's lifecycle value after the change (see TimeWindowState.ToApiValue); only "cancelled" is allowed. */
    [Required, RegularExpression("^" + Cancelled + "$")]
    public string Lifecycle { get; set; } = null!;

    public const string Cancelled = "cancelled";
}
