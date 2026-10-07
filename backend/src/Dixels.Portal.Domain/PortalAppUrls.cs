namespace Dixels.Portal;

/* Names of the addresses registered in ABP's AppUrlOptions (PortalWebModule.ConfigureUrls), used for links in emails.
 * Mvc = this server (sign-in and set-password pages, App:SelfUrl); Spa = the portal itself (App:ClientUrl). */
public static class PortalAppUrls
{
    public const string Mvc = "MVC";
    public const string Spa = "SPA";

    /* The portal's "My bookings" page, relative to the Spa root. */
    public const string BookingsPage = "app/bookings";

    /* The public page where an outside guest answers an invitation: {Spa}/respond/{secret}?answer=accept. */
    public const string RespondPage = "respond";
}
