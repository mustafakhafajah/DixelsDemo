using System;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace Dixels.Portal.Emailing;

/* Keeps this process under ExchangeOnlineThrottleOptions: every send first waits for a slot in the rate (oldest
 * waiter first) and then for a free connection. Covers queued and direct sends alike, since both go through the
 * sender. It counts this process only; a second copy of the portal sending from the same mailbox would need its own share. */
public sealed class ExchangeSendThrottle : ISingletonDependency, IDisposable
{
    private readonly SlidingWindowRateLimiter _rate;
    private readonly SemaphoreSlim _connections;

    public ExchangeSendThrottle(IOptions<ExchangeOnlineThrottleOptions> options)
    {
        _rate = new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
        {
            PermitLimit = options.Value.MessagesPerMinute,
            Window = TimeSpan.FromMinutes(1),
            SegmentsPerWindow = 6,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = int.MaxValue,
            AutoReplenishment = true
        });
        _connections = new SemaphoreSlim(options.Value.MaxConcurrentConnections);
    }

    /* Dispose the result when the connection is closed. */
    public async Task<IDisposable> AcquireAsync(CancellationToken cancellationToken = default)
    {
        using (var lease = await _rate.AcquireAsync(1, cancellationToken))
        {
            if (!lease.IsAcquired)
            {
                throw new EmailDeliveryException("Too many emails are waiting to be sent; try again shortly.", isTransient: true);
            }
        }

        await _connections.WaitAsync(cancellationToken);
        return new ConnectionSlot(_connections);
    }

    public void Dispose()
    {
        _rate.Dispose();
        _connections.Dispose();
    }

    private sealed class ConnectionSlot(SemaphoreSlim connections) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0) connections.Release();
        }
    }
}
