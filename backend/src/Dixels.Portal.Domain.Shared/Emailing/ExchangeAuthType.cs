namespace Dixels.Portal.Emailing;

/* How the portal signs in to the mail server (the ExchangeOnline.AuthType setting).
 * OAuth2: an Entra ID app registration with SMTP.SendAsApp; the way Microsoft 365 expects.
 * Basic: a user name and password (ABP's Abp.Mailing.Smtp.UserName / Password), or no sign-in at all when the user name
 * is empty. Microsoft 365 turns Basic off by default from the end of 2026, so this is for other SMTP servers and the
 * local Mailpit. */
public enum ExchangeAuthType
{
    OAuth2,
    Basic
}
