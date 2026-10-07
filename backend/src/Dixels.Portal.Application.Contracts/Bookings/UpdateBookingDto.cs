using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Dixels.Portal.Bookings;

/* PATCH /api/app/bookings/{id}. Either a new time, { startUtc, endUtc, expectedVersion }, or a new
 * lifecycle state: "cancelled" cancels the booking, "ended" ends it now and frees the space. */
public class UpdateBookingDto : IValidatableObject
{
    public const string Cancelled = "cancelled";
    public const string Ended = "ended";

    public DateTime? StartUtc { get; set; }
    public DateTime? EndUtc { get; set; }
    /* Optimistic concurrency for a new time: refused if the booking changed after the client loaded it. */
    public int? ExpectedVersion { get; set; }
    public string? Lifecycle { get; set; }
    /* With "cancelled": an optional subject and note for the cancellation emails. */
    public CancellationMessageDto? Message { get; set; }

    public bool IsReschedule => Lifecycle == null;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Lifecycle != null)
        {
            if (Lifecycle != Cancelled && Lifecycle != Ended)
                yield return new ValidationResult($"lifecycle can only be \"{Cancelled}\" or \"{Ended}\".", new[] { nameof(Lifecycle) });
            if (StartUtc.HasValue || EndUtc.HasValue)
                yield return new ValidationResult("Change the time or the lifecycle, not both at once.", new[] { nameof(Lifecycle) });
        }
        else if (!StartUtc.HasValue || !EndUtc.HasValue)
        {
            yield return new ValidationResult("A new time needs both startUtc and endUtc.", new[] { nameof(StartUtc), nameof(EndUtc) });
        }
    }
}
