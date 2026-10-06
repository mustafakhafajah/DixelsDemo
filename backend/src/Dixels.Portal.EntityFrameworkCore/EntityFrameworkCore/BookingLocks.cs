using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Bookings;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;

namespace Dixels.Portal.EntityFrameworkCore;

/* PostgreSQL transaction-scoped advisory locks (pg_advisory_xact_lock): the database releases them itself at
 * commit or rollback, so nothing can leave one behind. Keyed by a 64-bit hash of "owner:<id>" / "space:<id>";
 * two keys sharing a hash only means those two requests wait for each other. Other databases (the SQLite
 * tests) have no such locks and run one request at a time anyway, so there it does nothing. */
public class BookingLocks : IBookingLocks, ITransientDependency
{
    private readonly IDbContextProvider<PortalDbContext> _dbContextProvider;

    public BookingLocks(IDbContextProvider<PortalDbContext> dbContextProvider)
    {
        _dbContextProvider = dbContextProvider;
    }

    public Task LockOwnerAsync(Guid ownerUserId) => LockAsync(["owner:" + ownerUserId]);

    /* Always in the same (sorted) order, so two requests locking overlapping sets of spaces can't deadlock. */
    public Task LockSpacesAsync(IEnumerable<Guid> spaceIds)
        => LockAsync(spaceIds.Distinct().Order().Select(id => "space:" + id).ToArray());

    private async Task LockAsync(string[] keys)
    {
        if (keys.Length == 0) return;
        var db = await _dbContextProvider.GetDbContextAsync();
        if (!db.Database.IsNpgsql()) return;
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended(k, 0)) FROM unnest({keys}) AS k");
    }
}
