namespace Dixels.Portal.Settings;

/* Sending email through Microsoft 365 (Exchange Online). On the server these go in the git-ignored
 * appsettings.secrets.json under "Settings", e.g. "Settings": { "ExchangeOnline.Enabled": "true", ... };
 * docs/DEPLOYMENT.md has the full block. Server address and port are ABP's own Abp.Mailing.Smtp.* settings, which
 * ExchangeOnlineSettingDefinitionProvider points at Exchange Online by default. */
public static class ExchangeOnlineSettingNames
{
    private const string Prefix = "ExchangeOnline";

    /* Off by default: emails are only written to the log, so nothing is sent from a machine that isn't set up for it. */
    public const string Enabled = Prefix + ".Enabled";

    /* "OAuth2" (default) or "Basic"; see ExchangeAuthType. */
    public const string AuthType = Prefix + ".AuthType";

    /* The Entra ID (Azure AD) tenant and the app registration the portal signs in as (OAuth2 only). */
    public const string TenantId = Prefix + ".TenantId";
    public const string ClientId = Prefix + ".ClientId";
    public const string ClientSecret = Prefix + ".ClientSecret";

    /* The mailbox every email is sent from, e.g. noreply@dixels.io. With OAuth2 it is also the SMTP sign-in name. */
    public const string ServiceMailbox = Prefix + ".ServiceMailbox";

    /* Other From addresses the mailbox may send as (aliases, or mailboxes it has Send As on), comma-separated. */
    public const string AllowedFromAddresses = Prefix + ".AllowedFromAddresses";
}
