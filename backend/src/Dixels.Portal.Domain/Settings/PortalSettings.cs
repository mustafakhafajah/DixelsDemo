namespace Dixels.Portal.Settings;

public static class PortalSettings
{
    private const string Prefix = "Portal";

    /* The language a user picked in the portal. Kept apart from ABP's default-language setting so ABP's own
     * pages (sign-in and the like) are not affected by it. */
    public const string Language = Prefix + ".Language";
}
