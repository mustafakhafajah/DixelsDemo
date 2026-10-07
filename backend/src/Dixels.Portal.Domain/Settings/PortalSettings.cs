namespace Dixels.Portal.Settings;

public static class PortalSettings
{
    private const string Prefix = "Portal";

    /* The language a user picked in the portal. Kept apart from ABP's default-language setting so ABP's own
     * pages (sign-in and the like) are not affected by it. */
    public const string Language = Prefix + ".Language";

    /* The time zone a user wants booking emails in (an IANA name such as "Asia/Amman"). Not set: each email uses
     * the building's own zone. */
    public const string TimeZone = Prefix + ".TimeZone";
}
