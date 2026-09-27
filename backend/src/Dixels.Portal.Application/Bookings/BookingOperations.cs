using System;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Common;
using Dixels.Portal.Estate;
using Dixels.Portal.Maintenance;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Timing;

namespace Dixels.Portal.Bookings;

/* Steps more than one booking handler needs (e.g. both "cancel" and "cancel series" cancel one booking
 * the same way), kept in one place so each rule is written once. */
public class BookingOperations : ITransientDependency
{
    private readonly IRepository<Booking, Guid> _bookings;
    private readonly BookingManager _manager;
    private readonly MaintenanceScopeResolver _scopes;
    private readonly IPortalUserContext _user;
    private readonly IClock _clock;

    public BookingOperations(IRepository<Booking, Guid> bookings, BookingManager manager, MaintenanceScopeResolver scopes,
        IPortalUserContext user, IClock clock)
    {
        _bookings = bookings;
        _manager = manager;
        _scopes = scopes;
        _user = user;
        _clock = clock;
    }

    public async Task<Booking> GetAsync(Guid id)
        => await _bookings.FindAsync(id)
           ?? throw new BusinessException(PortalDomainErrorCodes.BookingNotFound, "No booking with that ID.");

    /* Owners act on their own bookings; admins on anyone's. */
    public async Task EnsureCanActAsync(Booking b, string message)
    {
        if (b.OwnerUserId != _user.UserIdOrNull && !await _user.IsAdminAsync())
            throw new BusinessException(PortalDomainErrorCodes.AccessForbidden, message);
    }

    public async Task<Booking> CreateOneAsync(Guid spaceId, DateTime startUtc, DateTime endUtc, bool parking,
        Guid? seriesId, string? idempotencyKey)
    {
        /* Parking is an admin-only extra: who is asking is an application concern, so it's decided here. */
        var isAdmin = await _user.IsAdminAsync();
        var booking = await _manager.CreateAsync(spaceId, _user.UserId, startUtc.AsUtc(), endUtc.AsUtc(),
            parking: isAdmin && parking, seriesId: seriesId, idempotencyKey: idempotencyKey);
        await _bookings.InsertAsync(booking, autoSave: true);
        return booking;
    }

    public async Task CancelOneAsync(Booking b)
    {
        await EnsureCanActAsync(b, "You can only cancel your own bookings.");
        var state = b.GetLifecycle(_clock.Now);
        if (state == "ended")
            throw new BusinessException(PortalDomainErrorCodes.BookingLocked, "An ended booking cannot be cancelled.");
        if (state == "cancelled") return;
        b.Cancel();
        await _bookings.UpdateAsync(b, autoSave: true);
    }

    /* "Upcoming" = confirmed and not started yet; a meeting already in progress is never cut off. */
    public async Task<IQueryable<Booking>> UpcomingInScopeAsync(EstateScopeDto scope)
    {
        var spaceIds = await _scopes.FindSpaceIdsAsync(scope.ScopeType, scope.ScopeId);
        var now = _clock.Now;
        return (await _bookings.GetQueryableAsync())
            .Where(b => spaceIds.Contains(b.SpaceId) && b.Status == BookingStatus.Confirmed && b.StartUtc > now);
    }
}
