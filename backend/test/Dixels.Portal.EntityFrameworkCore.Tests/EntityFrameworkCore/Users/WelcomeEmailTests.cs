using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Emailing;
using Volo.Abp.Identity;
using Xunit;

namespace Dixels.Portal.EntityFrameworkCore.Users;

/* A new account gets a welcome email, however it was created (here through ABP's user manager, which both the admin
 * Users page and self sign-up use). Tests don't run background jobs, so the queued email is read from the job table. */
[Collection(PortalTestConsts.CollectionDefinitionName)]
public class WelcomeEmailTests : PortalEntityFrameworkCoreTestBase
{
    [Fact]
    public async Task A_new_account_gets_one_welcome_email()
    {
        var email = $"new-{Guid.NewGuid():N}@dixels.test";

        await WithUnitOfWorkAsync(async () =>
            (await GetRequiredService<IdentityUserManager>().CreateAsync(new IdentityUser(Guid.NewGuid(), email.Split('@')[0], email) { Name = "Omar" }, "1q2w3E*Abc"))
                .Succeeded.ShouldBeTrue());

        var welcome = (await QueuedEmailsToAsync(email)).ShouldHaveSingleItem();
        welcome.Subject.ShouldStartWith("Welcome to Dixels Portal");
        welcome.Body.ShouldContain("Hello Omar,");
    }

    private Task<BackgroundEmailSendingJobArgs[]> QueuedEmailsToAsync(string to)
        => WithUnitOfWorkAsync(async () =>
        {
            var name = BackgroundJobNameAttribute.GetName<BackgroundEmailSendingJobArgs>();
            var serializer = GetRequiredService<IBackgroundJobSerializer>();
            return (await GetRequiredService<IBackgroundJobRepository>().GetListAsync())
                .Where(j => j.JobName == name)
                .Select(j => (BackgroundEmailSendingJobArgs)serializer.Deserialize(j.JobArgs, typeof(BackgroundEmailSendingJobArgs)))
                .Where(a => a.To == to)
                .ToArray();
        });
}
