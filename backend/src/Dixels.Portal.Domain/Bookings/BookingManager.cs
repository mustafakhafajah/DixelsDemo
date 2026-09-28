using System;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Buildings;
using Dixels.Portal.Estate;
using Dixels.Portal.Floors;
using Dixels.Portal.Maintenance;
using Dixels.Portal.Spaces;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace Dixels.Portal.Bookings;

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

    /* Factory: the only way to make a new booking, so no caller can skip the rules. */
    /* ownerMayHoldSeveralSpaces: the owner may have other spaces booked at the same time (admins may; who is
     * an admin is an application concern, so the caller decides). */
    public async Task<Booking> CreateAsync(Guid spaceId, Guid ownerId, DateTime startUtc, DateTime endUtc,
        Guid? seriesId = null, string? idempotencyKey = null, bool ownerMayHoldSeveralSpaces = false)
    {
        var ctx = await GetSpaceContextAsync(spaceId);
        await ValidateNewBookingAsync(ctx, ownerId, startUtc, endUtc, ownerMayHoldSeveralSpaces);
        return new Booking(GuidGenerator.Create(), spaceId, ownerId, startUtc, endUtc)
        {
            SeriesId = seriesId,
            IdempotencyKey = idempotencyKey,
        };
    }

    public async Task<SpaceContext> GetSpaceContextAsync(Guid spaceId)
    {
        var space = await _spaces.FindAsync(spaceId)
            ?? throw new UserFriendlyException(code: PortalDomainErrorCodes.SpaceNotFound, message: "No space with that ID.");
        var building = await _buildings.GetAsync(space.BuildingId);
        var floor = await _floors.FindAsync(space.FloorId);
        return new SpaceContext(space, floor, building);
    }

    public void ValidateWindow(SpaceContext ctx, DateTime startUtc, DateTime endUtc, bool allowPast = false)
    {
        if (startUtc == default || endUtc == default)
            throw new UserFriendlyException(code: PortalDomainErrorCodes.MissingField, message: "Start and end are both required.");
        if (endUtc <= startUtc)
            throw new UserFriendlyException(code: PortalDomainErrorCodes.EndBeforeStart, message: "End must be after start.");
        if (!allowPast && startUtc < Clock.Now)
            throw new UserFriendlyException(code: PortalDomainErrorCodes.StartInPast, message: "Start must not be in the past.");

        var c = ctx.Constraints;
        var day = DateOnly.FromDateTime(startUtc);
        if (c.Holidays.Contains(day))
            throw new UserFriendlyException(code: PortalDomainErrorCodes.HolidayClosed, message:
                $"{ctx.Space.Name}'s building is closed for a holiday on {day:yyyy-MM-dd}.");

        var sMin = startUtc.Hour * 60 + startUtc.Minute;
        var eMin = endUtc.Hour * 60 + endUtc.Minute;
        if (eMin == 0) eMin = 1440;
        if (sMin < c.OpenMinute || eMin > c.CloseMinute || endUtc.Date > startUtc.Date && eMin != 1440)
            throw new UserFriendlyException(code: PortalDomainErrorCodes.OutsideHours, message:
                $"{ctx.Space.Name} can only be booked between {c.OpenMinute / 60:00}:00 and {c.CloseMinute / 60:00}:00.");

        var mins = (endUtc - startUtc).TotalMinutes;
        if (mins < c.MinBookingMinutes)
            throw new UserFriendlyException(code: PortalDomainErrorCodes.DurationBelowMin, message:
                $"Minimum booking length here is {c.MinBookingMinutes} minutes.");
        if (mins > c.MaxBookingHours * 60)
            throw new UserFriendlyException(code: PortalDomainErrorCodes.DurationAboveMax, message:
                $"Maximum booking length here is {c.MaxBookingHours} hours.");
    }

    public void EnsureBookable(SpaceContext ctx)
    {
        var blocker = FindBookingBlocker(ctx);
        if (blocker != null) throw blocker;
    }

    public static bool CanBook(SpaceContext ctx) => FindBookingBlocker(ctx) == null;

    /* The sentence the UI shows next to a space that can't be booked, e.g. "HQ North is not bookable". */
    public static string? FindNotBookableReason(SpaceContext ctx) => FindBookingBlocker(ctx)?.Message;

    /* The one list of reasons a space can't be booked, shared by the yes/no check, the reason text and the
     * throwing check. The building is checked first, so the message names the level that actually blocks it. */
    private static BusinessException? FindBookingBlocker(SpaceContext ctx)
    {
        if (!ctx.Building.IsBookable)
            return new UserFriendlyException(code: PortalDomainErrorCodes.BuildingNotBookable, message:
                $"{ctx.Building.Name} is not bookable, so none of its spaces can be booked.");
        if (ctx.Floor != null && !ctx.Floor.IsBookable)
            return new UserFriendlyException(code: PortalDomainErrorCodes.FloorNotBookable, message:
                $"{ctx.Building.Name} · Floor {ctx.Floor.Name} is not bookable, so none of its spaces can be booked.");
        if (!ctx.Space.IsBookable)
            return new UserFriendlyException(code: PortalDomainErrorCodes.SpaceNotBookable, message:
                $"{ctx.Space.Name} is not bookable.");
        return null;
    }

    public async Task EnsureNoMaintenanceAsync(SpaceContext ctx, DateTime startUtc, DateTime endUtc)
    {
        var m = await _maintenance.FirstOrDefaultAsync(new OverlappingMaintenanceSpecification(startUtc, endUtc)
            .ToExpression().And(x => x.SpaceId == ctx.Space.Id));
        if (m != null)
            throw new UserFriendlyException(code: PortalDomainErrorCodes.SpaceUnderMaintenance, message:
                    $"{ctx.Space.Name} is blocked ({m.Note ?? "blocked time"}) from {Stamp(m.StartUtc, m.EndUtc)} and can't be booked then.")
                .WithData("maintenanceId", m.Id)
                .WithData("scope", m.ScopeType.ToString());
    }

    public async Task EnsureNoConflictAsync(Guid spaceId, DateTime startUtc, DateTime endUtc, Guid? excludeId)
    {
        var c = await _bookings.FirstOrDefaultAsync(new OverlappingBookingsSpecification(startUtc, endUtc)
            .ToExpression().And(b => b.SpaceId == spaceId && b.Id != excludeId));
        if (c != null)
            throw new UserFriendlyException(code: PortalDomainErrorCodes.BookingConflict, message:
                    "The space is already booked for part of that window.")
                .WithData("conflictingBookingId", c.Id)
                .WithData("conflictStart", c.StartUtc.ToString("o"))
                .WithData("conflictEnd", c.EndUtc.ToString("o"));
    }

    /* One person can't hold two different spaces at once. */
    public async Task EnsureNoSelfOverlapAsync(Guid userId, Guid spaceId, DateTime startUtc, DateTime endUtc, Guid? excludeId)
    {
        var so = await _bookings.FirstOrDefaultAsync(new OverlappingBookingsSpecification(startUtc, endUtc)
            .ToExpression().And(b => b.OwnerUserId == userId && b.SpaceId != spaceId && b.Id != excludeId));
        if (so != null)
        {
            var other = await _spaces.FindAsync(so.SpaceId);
            throw new UserFriendlyException(code: PortalDomainErrorCodes.BookingSelfOverlap, message:
                    $"You already have {other?.Name ?? "another space"} booked from {Hm(so.StartUtc)} to {Hm(so.EndUtc)}.")
                .WithData("conflictingBookingId", so.Id)
                .WithData("conflictingSpace", so.SpaceId);
        }
    }

    /* Full create-time check chain in the mock's order. */
    private async Task ValidateNewBookingAsync(SpaceContext ctx, Guid ownerId, DateTime startUtc, DateTime endUtc,
        bool ownerMayHoldSeveralSpaces)
    {
        ValidateWindow(ctx, startUtc, endUtc);
        EnsureBookable(ctx);
        await EnsureNoMaintenanceAsync(ctx, startUtc, endUtc);
        await EnsureNoConflictAsync(ctx.Space.Id, startUtc, endUtc, null);
        if (!ownerMayHoldSeveralSpaces)
            await EnsureNoSelfOverlapAsync(ownerId, ctx.Space.Id, startUtc, endUtc, null);
    }

    private static string Hm(DateTime d) => d.ToString("HH:mm");

    /* "09:00 to 11:00" on one day, "2026-10-01 09:00 to 2026-10-21 18:00" across days (blocked time can be long). */
    private static string Stamp(DateTime s, DateTime e) => s.Date == e.Date
        ? $"{Hm(s)} to {Hm(e)}"
        : $"{s:yyyy-MM-dd HH:mm} to {e:yyyy-MM-dd HH:mm}";
}
