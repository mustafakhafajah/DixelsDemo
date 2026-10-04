using System.Threading.Tasks;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;

namespace Dixels.Portal.Emailing;

/* Queues emails for SendEmailJob through ABP's background-job manager (stored in the AbpBackgroundJobs table). */
public class ExchangeEmailJobManager : ITransientDependency
{
    private readonly IBackgroundJobManager _jobs;

    public ExchangeEmailJobManager(IBackgroundJobManager jobs)
    {
        _jobs = jobs;
    }

    /* False when background jobs are switched off (e.g. in tests); callers then send straight away. */
    public bool IsAvailable() => _jobs.IsAvailable();

    public Task<string> EnqueueAsync(SendEmailJobArgs args) => _jobs.EnqueueAsync(args);
}
