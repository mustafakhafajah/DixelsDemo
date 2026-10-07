using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace Dixels.Portal.Bookings;

/* Every hour (and once when the site starts): wipes the guest addresses of bookings that are over (BookingGuestCleaner).
 * It runs inside the website, so while IIS has the site asleep it waits and catches up when the site wakes. */
public class BookingGuestCleanupWorker : AsyncPeriodicBackgroundWorkerBase
{
    public static readonly TimeSpan Period = TimeSpan.FromHours(1);

    public BookingGuestCleanupWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory)
        : base(timer, serviceScopeFactory)
    {
        Timer.Period = (int)Period.TotalMilliseconds;
        Timer.RunOnStart = true;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        var wiped = await workerContext.ServiceProvider.GetRequiredService<BookingGuestCleaner>().ForgetFinishedAsync();
        if (wiped > 0) Logger.LogInformation("Wiped {Count} guest email address(es) from bookings that are over.", wiped);
    }
}
