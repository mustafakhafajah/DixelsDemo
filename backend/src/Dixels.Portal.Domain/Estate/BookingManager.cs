using System;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Buildings;
using Dixels.Portal.Floors;
using Dixels.Portal.Spaces;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace Dixels.Portal.Estate;

/* Booking business rules, ported from the mock's api.js rules engine. */
public class BookingManager : DomainService
{
    private readonly IRepository<Space, Guid> _spaces;
    private readonly IRepository<Floor, Guid> _floors;
    private readonly IRepository<Building, Guid> _buildings;
    private readonly IRepository<Booking, Guid> _bookings;
    private readonly IRepository<MaintenanceWindow, Guid> _maintenance;

    public BookingManager(
        IRepository<Space, Guid> spaces,
        IRepository<Floor, Guid> floors,
        IRepository<Building, Guid> buildings,
        IRepository<Booking, Guid> bookings,
        IRepository<MaintenanceWindow, Guid> maintenance)
    {
        _spaces = spaces;
        _floors = floors;
        _buildings = buildings;
        _bookings = bookings;
        _maintenance = maintenance;
    }

    public static string Hm(DateTime d) => d.ToString("HH:mm");
    public static string Stamp(DateTime d) => d.ToString("yyyy-MM-dd HH:mm");

    public async Task<SpaceContext> GetSpaceContextAsync(Guid spaceId)
    {
        var space = await _spaces.FindAsync(spaceId)
            ?? throw new BusinessException(PortalDomainErrorCodes.SpaceNotFound, "No space with that ID.");
        var building = await _buildings.GetAsync(space.BuildingId);
        var floor = await _floors.FindAsync(space.FloorId);
        return new SpaceContext(space, floor, building);
    }

    public void ValidateWindow(SpaceContext ctx, DateTime startUtc, DateTime endUtc, bool allowPast = false)
    {
        if (startUtc == default || endUtc == default)
            throw new BusinessException(PortalDomainErrorCodes.MissingField, "Start and end are both required.");
        if (endUtc <= startUtc)
            throw new BusinessException(PortalDomainErrorCodes.EndBeforeStart, "End must be after start.");
        if (!allowPast && startUtc < Clock.Now)
            throw new BusinessException(PortalDomainErrorCodes.StartInPast, "Start must not be in the past.");

        var c = ctx.Constraints;
        var day = DateOnly.FromDateTime(startUtc);
        if (c.Holidays.Contains(day))
            throw new BusinessException(PortalDomainErrorCodes.HolidayClosed,
                $"{ctx.Space.Name}'s building is closed for a holiday on {day:yyyy-MM-dd}.");

        var sMin = startUtc.Hour * 60 + startUtc.Minute;
        var eMin = endUtc.Hour * 60 + endUtc.Minute;
        if (eMin == 0) eMin = 1440;
        if (sMin < c.OpenMinute || eMin > c.CloseMinute || endUtc.Date > startUtc.Date && eMin != 1440)
            throw new BusinessException(PortalDomainErrorCodes.OutsideHours,
                $"{ctx.Space.Name} can only be booked between {c.OpenMinute / 60:00}:00 and {c.CloseMinute / 60:00}:00.");

        var mins = (endUtc - startUtc).TotalMinutes;
        if (mins < c.MinBookingMinutes)
            throw new BusinessException(PortalDomainErrorCodes.DurationBelowMin,
                $"Minimum booking length here is {c.MinBookingMinutes} minutes.");
        if (mins > c.MaxBookingHours * 60)
            throw new BusinessException(PortalDomainErrorCodes.DurationAboveMax,
                $"Maximum booking length here is {c.MaxBookingHours} hours.");
    }

    public void EnsureBookable(SpaceContext ctx)
    {
        if (ctx.Building.Status != EstateStatus.Active)
            throw new BusinessException(PortalDomainErrorCodes.BuildingInactive,
                $"{ctx.Building.Name} is inactive and cannot be booked.");
        if (ctx.Floor != null && ctx.Floor.Status != EstateStatus.Active)
            throw new BusinessException(PortalDomainErrorCodes.FloorInactive,
                $"{ctx.Building.Name} · Floor {ctx.Floor.Name} is inactive and cannot be booked.");
        if (ctx.Space.Status != EstateStatus.Active)
            throw new BusinessException(PortalDomainErrorCodes.SpaceInactive,
                "This space is inactive and cannot be booked.");
    }

    public static bool CanBook(SpaceContext ctx)
        => ctx.Building.Status == EstateStatus.Active &&
           (ctx.Floor == null || ctx.Floor.Status == EstateStatus.Active) &&
           ctx.Space.Status == EstateStatus.Active;

    public async Task EnsureNoMaintenanceAsync(SpaceContext ctx, DateTime startUtc, DateTime endUtc)
    {
        var m = await _maintenance.FirstOrDefaultAsync(x =>
            x.SpaceId == ctx.Space.Id && x.Status == MaintenanceStatus.Active &&
            x.StartUtc < endUtc && startUtc < x.EndUtc);
        if (m != null)
            throw new BusinessException(PortalDomainErrorCodes.SpaceUnderMaintenance,
                    $"{ctx.Space.Name} is scheduled for cleaning {Hm(m.StartUtc)}–{Hm(m.EndUtc)} and can't be booked then.")
                .WithData("maintenanceId", m.Id)
                .WithData("scope", m.ScopeType.ToString());
    }

    public async Task EnsureNoConflictAsync(Guid spaceId, DateTime startUtc, DateTime endUtc, Guid? excludeId)
    {
        var c = await _bookings.FirstOrDefaultAsync(b =>
            b.SpaceId == spaceId && b.Status == BookingStatus.Confirmed && b.Id != excludeId &&
            b.StartUtc < endUtc && startUtc < b.EndUtc);
        if (c != null)
            throw new BusinessException(PortalDomainErrorCodes.BookingConflict,
                    "The space is already booked for part of that window.")
                .WithData("conflictingBookingId", c.Id)
                .WithData("conflictStart", c.StartUtc.ToString("o"))
                .WithData("conflictEnd", c.EndUtc.ToString("o"));
    }

    /* One person can't hold two different spaces at once. */
    public async Task EnsureNoSelfOverlapAsync(Guid userId, Guid spaceId, DateTime startUtc, DateTime endUtc, Guid? excludeId)
    {
        var so = await _bookings.FirstOrDefaultAsync(b =>
            b.OwnerUserId == userId && b.SpaceId != spaceId && b.Status == BookingStatus.Confirmed &&
            b.Id != excludeId && b.StartUtc < endUtc && startUtc < b.EndUtc);
        if (so != null)
        {
            var other = await _spaces.FindAsync(so.SpaceId);
            throw new BusinessException(PortalDomainErrorCodes.BookingSelfOverlap,
                    $"You already have {other?.Name ?? "another space"} booked from {Hm(so.StartUtc)} to {Hm(so.EndUtc)}.")
                .WithData("conflictingBookingId", so.Id)
                .WithData("conflictingSpace", so.SpaceId);
        }
    }

    /* Full create-time check chain in the mock's order. */
    public async Task ValidateNewBookingAsync(SpaceContext ctx, Guid ownerId, DateTime startUtc, DateTime endUtc)
    {
        ValidateWindow(ctx, startUtc, endUtc);
        EnsureBookable(ctx);
        await EnsureNoMaintenanceAsync(ctx, startUtc, endUtc);
        await EnsureNoConflictAsync(ctx.Space.Id, startUtc, endUtc, null);
        await EnsureNoSelfOverlapAsync(ownerId, ctx.Space.Id, startUtc, endUtc, null);
    }
}
