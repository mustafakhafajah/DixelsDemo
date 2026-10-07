using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Dixels.Portal.Bookings;

/* One person to invite: a portal user picked from the list (userId), or anyone else by email address. */
public class AttendeeInputDto
{
    public Guid? UserId { get; set; }
    [StringLength(BookingConsts.MaxAttendeeEmailLength)]
    public string? Email { get; set; }
}

/* PUT /api/app/bookings/{id}/attendees: the whole new list. People already invited and still on it get no email;
 * the ones added are invited and the ones left out are told. */
public class SetBookingAttendeesDto
{
    [MaxLength(BookingConsts.MaxAttendees)]
    public List<AttendeeInputDto> Attendees { get; set; } = new();
}

/* A portal user someone invited, as the booking shows them. */
public class BookingAttendeeDto
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = null!;
    public string? Email { get; set; }
    /* Their answer: "none" (not answered yet), "accepted", "tentative" or "declined". Someone who declined is still
     * listed, so the owner sees it and they can change their mind. */
    public string Response { get; set; } = "none";
    public DateTime? RespondedAt { get; set; }
}

/* An outside guest as the owner sees them: their address and their answer (from their private answer link). */
public class BookingGuestDto
{
    public string Email { get; set; } = null!;
    public string Response { get; set; } = "none";
    public DateTime? RespondedAt { get; set; }
}

/* GET /api/app/booking-people: active users to pick from when inviting people. */
public class BookingPeopleFilterDto
{
    [StringLength(128)]
    public string? Filter { get; set; }
}

public class BookingPersonDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Email { get; set; }
}
