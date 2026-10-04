using static Dixels.Portal.Notifications.PortalEmailLayout;

namespace Dixels.Portal.Notifications;

/* The welcome email for a new account, in English. Pure, like BookingEmails. */
public static class WelcomeEmails
{
    /* setPasswordUrl: only for an account an admin created, so the person picks their own password.
     * Someone who signed up themselves already has one and just gets the portal link. */
    public static EmailContent Welcome(string name, string userName, string? setPasswordUrl, string? portalUrl)
    {
        if (setPasswordUrl != null)
        {
            return new EmailContent("Welcome to Dixels Portal: set your password", Html("Welcome to Dixels Portal",
                [$"Hello {Encode(name)},",
                 "An account has been created for you, so you can book desks, rooms and other spaces.",
                 $"Your username is <strong>{Encode(userName)}</strong>. Choose your own password with the button below; the link works for a limited time.",
                 portalUrl != null
                     ? $"Then sign in at <a href=\"{Encode(portalUrl)}\">{Encode(portalUrl)}</a>. If the link has expired, use <em>Forgot password?</em> on the sign-in page."
                     : "If the link has expired, use <em>Forgot password?</em> on the sign-in page."],
                "Set my password", setPasswordUrl));
        }

        return new EmailContent("Welcome to Dixels Portal", Html("Welcome to Dixels Portal",
            [$"Hello {Encode(name)},",
             $"Your account <strong>{Encode(userName)}</strong> is ready. You can now book desks, rooms and other spaces."],
            "Open the portal", portalUrl));
    }
}
