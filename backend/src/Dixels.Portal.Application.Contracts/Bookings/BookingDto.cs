using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace Dixels.Portal.Bookings;

public class BookingDto : EntityDto<Guid>
{
    public Guid SpaceId { get; set; }
    public string SpaceName { get; set; } = null!;
    public Guid OwnerUserId { get; set; }
    public string OwnerName { get; set; } = null!;
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public BookingStatus Status { get; set; }
    public string Lifecycle { get; set; } = null!;
    public int Version { get; set; }
    public Guid? SeriesId { get; set; }
    public DateTime CreationTime { get; set; }
    public DateTime? LastModificationTime { get; set; }
    /* The portal users invited. */
    public List<BookingAttendeeDto> Attendees { get; set; } = new();
    /* Outside guests (address and answer): only for the owner and people who see everyone's bookings, and only until
     * the booking is over or cancelled. GuestCount always says how many there are. */
    public List<BookingGuestDto> Guests { get; set; } = new();
    public int GuestCount { get; set; }
}
