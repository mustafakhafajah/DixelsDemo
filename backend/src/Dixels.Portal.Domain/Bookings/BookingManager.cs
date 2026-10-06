using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Buildings;
using Dixels.Portal.Estate;
using Dixels.Portal.Floors;
using Dixels.Portal.Maintenance;
using Dixels.Portal.Spaces;
using Microsoft.Extensions.Localization;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;

namespace Dixels.Portal.Bookings;

/* Booking business rules, ported from the mock's api.js rules engine. */
public class BookingManager : PortalDomainService
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
            ?? throw new UserFriendlyException(code: PortalDomainErrorCodes.SpaceNotFound, message: L["Error:SpaceNotFound"]).ForField("spaceId");
        var building = await _buildings.GetAsync(space.BuildingId);
        var floor = await _floors.FindAsync(space.FloorId);
        return new SpaceContext(space, floor, building);
    }

    public void ValidateWindow(SpaceContext ctx, DateTime startUtc, DateTime endUtc, bool allowPast = false)
    {
        var problem = FindWindowProblem(ctx, startUtc, endUtc, allowPast);
        if (problem != null) throw problem;
    }

    /* The first rule the window breaks (missing or reversed times, past start, closed day, opening hours,
     * min / max length), or null when it fits. ValidateWindow throws it; "Find a space" uses it to keep only
     * spaces that are free for a window without throwing per space. */
    public BusinessException? FindWindowProblem(SpaceContext ctx, DateTime startUtc, DateTime endUtc, bool allowPast = false)
    {
        if (startUtc == default || endUtc == default)
            return new UserFriendlyException(code: PortalDomainErrorCodes.MissingField, message: L["Error:StartEndRequired"]).ForField("start");
        if (endUtc <= startUtc)
            return new UserFriendlyException(code: PortalDomainErrorCodes.EndBeforeStart, message: L["Error:EndBeforeStart"]).ForField("end");
        if (!allowPast && startUtc < Clock.Now)
            return new UserFriendlyException(code: PortalDomainErrorCodes.StartInPast, message: L["Error:StartInPast"]).ForField("start");

        var c = ctx.Constraints;
        var closed = BuildingCalendar.FindClosedDay(c, startUtc, endUtc);
        if (closed != null)
            return new UserFriendlyException(code: PortalDomainErrorCodes.HolidayClosed, message: closed.IsHoliday
                ? L["Error:ClosedForHoliday", ctx.Space.GetName(), closed.Day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)]
                : L["Error:ClosedOnWeekday", ctx.Space.GetName(), L[$"Weekdays:{(int)closed.Day.DayOfWeek}"]]).ForField("date");

        if (!BuildingCalendar.IsWithinHours(c, startUtc, endUtc))
        {
            var open = $"{c.OpenMinute / 60:00}:00";
            var close = $"{c.CloseMinute / 60:00}:00";
            var zone = BuildingCalendar.Zone(c.TimeZone);
            if (BuildingCalendar.IsUtcLike(zone, startUtc))
                return new UserFriendlyException(code: PortalDomainErrorCodes.OutsideHours, message:
                    L["Error:OutsideHours", ctx.Space.GetName(), open, close]).ForField("window");
            /* Times are picked in UTC, so a building on another clock also gets its hours in UTC for that day. */
            var day = BuildingCalendar.LocalDay(startUtc, zone);
            var openUtc = BuildingCalendar.LocalToUtc(day, c.OpenMinute, zone).ToString("HH:mm", CultureInfo.InvariantCulture);
            var closeUtc = BuildingCalendar.LocalToUtc(day, c.CloseMinute, zone).ToString("HH:mm", CultureInfo.InvariantCulture);
            return new UserFriendlyException(code: PortalDomainErrorCodes.OutsideHours, message:
                L["Error:OutsideHoursZone", ctx.Space.GetName(), open, close, BuildingCalendar.ZoneCity(c.TimeZone), openUtc, closeUtc]).ForField("window");
        }

        var mins = (endUtc - startUtc).TotalMinutes;
        if (mins < c.MinBookingMinutes)
            return new UserFriendlyException(code: PortalDomainErrorCodes.DurationBelowMin, message:
                L["Error:DurationBelowMin", c.MinBookingMinutes]).ForField("window");
        if (mins > c.MaxBookingHours * 60)
            return new UserFriendlyException(code: PortalDomainErrorCodes.DurationAboveMax, message:
                L["Error:DurationAboveMax", c.MaxBookingHours]).ForField("window");
        return null;
    }

    public void EnsureBookable(SpaceContext ctx)
    {
        var blocker = FindBookingBlocker(ctx, L);
        if (blocker != null) throw blocker;
    }

    public static bool CanBook(SpaceContext ctx)
        => ctx.Building.IsBookable && ctx.Floor is not { IsBookable: false } && ctx.Space.IsBookable;

    /* The sentence the UI shows next to a space that can't be booked, e.g. "HQ North is not bookable",
     * in the language of the given localizer. */
    public static string? FindNotBookableReason(SpaceContext ctx, IStringLocalizer l) => FindBookingBlocker(ctx, l)?.Message;

    /* The one list of reasons a space can't be booked, shared by the reason text and the throwing check
     * (CanBook asks the same three questions). The building is checked first, so the message names the level
     * that actually blocks it. */
    private static BusinessException? FindBookingBlocker(SpaceContext ctx, IStringLocalizer l)
    {
        if (!ctx.Building.IsBookable)
            return new UserFriendlyException(code: PortalDomainErrorCodes.BuildingNotBookable, message:
                l["Error:BuildingNotBookable", ctx.Building.GetName()]).ForField("spaceId");
        if (ctx.Floor != null && !ctx.Floor.IsBookable)
            return new UserFriendlyException(code: PortalDomainErrorCodes.FloorNotBookable, message:
                l["Error:FloorNotBookable", ctx.Building.GetName(), ctx.Floor.GetName()]).ForField("spaceId");
        if (!ctx.Space.IsBookable)
            return new UserFriendlyException(code: PortalDomainErrorCodes.SpaceNotBookable, message:
                l["Error:SpaceNotBookable", ctx.Space.GetName()]).ForField("spaceId");
        return null;
    }

    public async Task EnsureNoMaintenanceAsync(SpaceContext ctx, DateTime startUtc, DateTime endUtc)
    {
        var m = await _maintenance.FirstOrDefaultAsync(new OverlappingMaintenanceSpecification(startUtc, endUtc)
            .ToExpression().And(x => x.SpaceId == ctx.Space.Id));
        if (m != null)
            throw new UserFriendlyException(code: PortalDomainErrorCodes.SpaceUnderMaintenance, message:
                    L["Error:SpaceUnderMaintenance", ctx.Space.GetName(), m.Note ?? L["BlockedTime"], Stamp(m.StartUtc, m.EndUtc)])
                .WithData("maintenanceId", m.Id)
                .WithData("scope", m.ScopeType.ToString())
                .WithData(ErrorFieldExtensions.FieldKey, "window");
    }

    public async Task EnsureNoConflictAsync(Guid spaceId, DateTime startUtc, DateTime endUtc, Guid? excludeId)
    {
        var c = await _bookings.FirstOrDefaultAsync(new OverlappingBookingsSpecification(startUtc, endUtc)
            .ToExpression().And(b => b.SpaceId == spaceId && b.Id != excludeId));
        if (c != null)
            throw new UserFriendlyException(code: PortalDomainErrorCodes.BookingConflict, message:
                    L["Error:BookingConflict"])
                .WithData("conflictingBookingId", c.Id)
                .WithData("conflictStart", c.StartUtc.ToString("o"))
                .WithData("conflictEnd", c.EndUtc.ToString("o"))
                .WithData(ErrorFieldExtensions.FieldKey, "window");
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
                    L["Error:BookingSelfOverlap", other?.GetName() ?? L["AnotherSpace"], Hm(so.StartUtc), Hm(so.EndUtc)])
                .WithData("conflictingBookingId", so.Id)
                .WithData("conflictingSpace", so.SpaceId)
                .WithData(ErrorFieldExtensions.FieldKey, "window");
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

    private static string Hm(DateTime d) => d.ToString("HH:mm", CultureInfo.InvariantCulture);

    /* "09:00 to 11:00" on one day, "2026-10-01 09:00 to 2026-10-21 18:00" across days (blocked time can be long). */
    private string Stamp(DateTime s, DateTime e) => s.Date == e.Date
        ? L["TimeRange", Hm(s), Hm(e)]
        : L["TimeRange", s.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), e.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)];
}
