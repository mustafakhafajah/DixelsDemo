using System;
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
    public bool Parking { get; set; }
    public DateTime CreationTime { get; set; }
    public DateTime? LastModificationTime { get; set; }
}
