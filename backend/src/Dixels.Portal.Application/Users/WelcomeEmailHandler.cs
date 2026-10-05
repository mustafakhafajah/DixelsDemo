using System;
using System.Threading.Tasks;
using Dixels.Portal.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities.Events;
using Volo.Abp.Emailing;
using Volo.Abp.EventBus;
using Volo.Abp.Identity;
using Volo.Abp.UI.Navigation.Urls;
using Volo.Abp.Users;

namespace Dixels.Portal.Users;

/* Welcomes every new account: ABP raises this event for any user created through its user manager, which covers both
 * an admin adding someone (Identity > Users) and self sign-up on the Register page.
 * Someone signed in creating another account = an admin: the email carries a link to ABP's own set-password page, so
 * the person chooses their own password. Nobody signed in = self sign-up: a plain welcome.
 * Lives in the Application layer, which the DbMigrator does not load, so seeded users (admin, employee) get nothing. */
public class WelcomeEmailHandler : ILocalEventHandler<EntityCreatedEventData<IdentityUser>>, ITransientDependency
{
    private readonly IEmailSender _emailSender;
    private readonly IdentityUserManager _userManager;
    private readonly IAppUrlProvider _urls;
    private readonly ICurrentUser _currentUser;

    public ILogger<WelcomeEmailHandler> Logger { get; set; } = NullLogger<WelcomeEmailHandler>.Instance;

    public WelcomeEmailHandler(IEmailSender emailSender, IdentityUserManager userManager, IAppUrlProvider urls, ICurrentUser currentUser)
    {
        _emailSender = emailSender;
        _userManager = userManager;
        _urls = urls;
        _currentUser = currentUser;
    }

    public async Task HandleEventAsync(EntityCreatedEventData<IdentityUser> eventData)
    {
        var user = eventData.Entity;
        if (string.IsNullOrWhiteSpace(user.Email)) return;
        try
        {
            var createdByAdmin = _currentUser.IsAuthenticated && _currentUser.Id != user.Id;
            var setPasswordUrl = createdByAdmin ? await SetPasswordUrlAsync(user) : null;
            var portalUrl = await _urls.GetUrlOrNullAsync(PortalAppUrls.Spa);
            var name = string.IsNullOrWhiteSpace(user.Name) ? user.GetDisplayName() : user.Name.Trim();

            var email = WelcomeEmails.Welcome(name, user.UserName, setPasswordUrl, portalUrl);
            await _emailSender.QueueAsync(user.Email, email.Subject, email.Html);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Could not queue the welcome email for user {UserId}.", user.Id);
        }
    }

    /* ABP's own reset-password page on this server, the same one "Forgot password?" links to. */
    private async Task<string?> SetPasswordUrlAsync(IdentityUser user)
    {
        var server = await _urls.GetUrlOrNullAsync(PortalAppUrls.Mvc);
        if (string.IsNullOrWhiteSpace(server)) return null;
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var tenant = user.TenantId.HasValue ? $"&tenantId={user.TenantId}" : "";
        return $"{server.TrimEnd('/')}/Account/ResetPassword?userId={user.Id}{tenant}&resetToken={Uri.EscapeDataString(token)}";
    }
}
