# Email through Microsoft 365 (Exchange Online): IT checklist

The portal sends its emails (password resets, notifications, test emails) through one Microsoft 365 mailbox, the
**service mailbox** (for example `noreply@dixels.io`). It signs in as an **Entra ID app registration** with OAuth2;
no mailbox password is stored anywhere.

Until step 5 is done, the portal **sends nothing**: emails are only written to its log (`ExchangeOnline.Enabled` is off by default).

You need: a Microsoft 365 admin (Exchange admin and Application administrator, or Global admin), access to the DNS of
the sending domain, and access to the server's `appsettings.secrets.json`.

---

## 1. Service mailbox (Exchange admin center)

1. Create the mailbox, e.g. `noreply@dixels.io`. A **shared mailbox** is enough (no license, no password).
2. Turn on **SMTP AUTH** for that mailbox only. The portal uses SMTP AUTH (with OAuth2) on port 587. In Exchange
   Online PowerShell:

   ```powershell
   Install-Module -Name ExchangeOnlineManagement      # once
   Connect-ExchangeOnline

   Set-CASMailbox -Identity noreply@dixels.io -SmtpClientAuthenticationDisabled $false
   Get-CASMailbox -Identity noreply@dixels.io | Format-List SmtpClientAuthenticationDisabled   # should say False
   ```

   The per-mailbox setting overrides the organisation one, so the organisation can keep SMTP AUTH off
   (`Get-TransportConfig | Format-List SmtpClientAuthenticationDisabled`).

## 2. App registration (Microsoft Entra admin center)

1. **App registrations → New registration**: name `Dixels Portal mail`, *Accounts in this organizational directory
   only*, no redirect URI.
2. Note the **Application (client) ID** and the **Directory (tenant) ID** from its Overview page.
3. **Certificates & secrets → New client secret.** Copy the **Value** straight away, because it is shown only once.
   Note the expiry date in a calendar: when the secret expires, email stops (see *AADSTS7000222* below).
4. **API permissions → Add a permission → APIs my organization uses →** search *Office 365 Exchange Online* →
   **Application permissions → `SMTP.SendAsApp`** → Add.
5. **Grant admin consent** for the organisation (the button on the same page). The status must turn green.

## 3. Let the app use the mailbox (Exchange Online PowerShell)

Exchange needs its own record of the app (a service principal) before it lets the app into a mailbox.

1. In the Entra admin center open **Enterprise applications** (not App registrations) → `Dixels Portal mail` and copy
   its **Object ID**. The one under App registrations is a different ID and will not work.
2. Run:

   ```powershell
   Connect-ExchangeOnline

   New-ServicePrincipal -AppId <APPLICATION_CLIENT_ID> -ObjectId <ENTERPRISE_APP_OBJECT_ID> -DisplayName "Dixels Portal mail"

   $sp = Get-ServicePrincipal -Identity "Dixels Portal mail"
   Add-MailboxPermission -Identity noreply@dixels.io -User $sp.Identity -AccessRights FullAccess
   ```

3. Only if the portal must send **from another address** (an alias or another mailbox), allow that too, then list it in
   `ExchangeOnline.AllowedFromAddresses` in step 5:

   ```powershell
   Add-RecipientPermission -Identity bookings@dixels.io -Trustee $sp.Identity -AccessRights SendAs
   ```

Changes can take up to an hour to apply. A `535 5.7.3` straight after this step is usually just that delay.

## 4. Security Defaults and Conditional Access

- **Security Defaults** switch off *Basic* authentication, which the portal does not use. Microsoft notes that SMTP
  AUTH as a whole is off while Security Defaults are on. If the test email fails with
  `5.7.139 … security defaults policy` while Security Defaults are on, move to Conditional Access, or ask Microsoft
  support; do not switch the portal to Basic.
- **Conditional Access**: the portal signs in as an *app* (client credentials), not as a user, so user policies such as
  MFA or compliant device don't apply to it. If you use *workload identity* policies, allow this app from the
  server's public IP.
- **Basic authentication for SMTP AUTH** is being switched off by default from the end of December 2026, and Microsoft
  will name the final shutdown date in 2027. The portal's default (`OAuth2`) is not affected. `Basic` exists for
  non-Microsoft SMTP servers and local testing only.

## 5. Server settings (`appsettings.secrets.json`)

Add a `"Settings"` block to the git-ignored `appsettings.secrets.json` next to the live site's
`Dixels.Portal.Web.dll` (`C:\Publish\DixelsPortalDemo`). The deploy script leaves `appsettings*.json` alone, so this
survives redeploys.

```json
{
  "Settings": {
    "ExchangeOnline.Enabled": "true",
    "ExchangeOnline.AuthType": "OAuth2",
    "ExchangeOnline.TenantId": "<Directory (tenant) ID>",
    "ExchangeOnline.ClientId": "<Application (client) ID>",
    "ExchangeOnline.ClientSecret": "<client secret Value>",
    "ExchangeOnline.ServiceMailbox": "noreply@dixels.io",
    "ExchangeOnline.AllowedFromAddresses": "",
    "Abp.Mailing.DefaultFromDisplayName": "Dixels Portal"
  }
}
```

- Keep any other content of the file (e.g. a connection string) and merge `"Settings"` in beside it.
- Also set the portal's address, so the emails' buttons ("View my bookings", "Open the portal") link to it. Put it in
  the server's `appsettings.json` under `"App"`: `"ClientUrl": "https://192.168.2.164/portal"`. Without it, the
  emails go out without those buttons.
- **Booking reminders** (10 minutes before a booking) are sent by the site itself, so it must keep running. In IIS,
  set the app pool's **Start Mode** to `AlwaysRunning` and **Idle Time-out** to `0`. Otherwise reminders stop while
  nobody uses the site. A reminder that comes due after the booking has started is dropped, not sent late.
- Server and port default to `smtp.office365.com`, `587`, STARTTLS. Nothing to set.
- Restart the site (recycle the IIS app pool) so it reads the file.
- Run the **DbMigrator** once after deploying this version. It gives the admin role the new
  *Email → Send a test email* permission.

## 6. Check it works

1. Sign in to Swagger (`https://<server>/server/swagger`) as an admin.
2. `POST /api/app/test-emails` with `{ "to": "you@dixels.io" }`. Optionally add `"replyTo"`.
3. The reply shows `succeeded`, the server's exact answer (`serverResponse`), the codes, and a plain-words `hint`.
   `"succeeded": true` with `serverResponse` like `2.0.0 OK …` means it works.

## 7. DNS for the sending domain (SPF, DKIM, DMARC)

Without these, the emails land in spam or are rejected by other companies' mail servers.

| Record | Type | Name | Value |
|---|---|---|---|
| SPF | TXT | `dixels.io` | `v=spf1 include:spf.protection.outlook.com -all`. If an SPF record already exists, add the `include:` to it; a domain must have only one. |
| DKIM | CNAME ×2 | `selector1._domainkey`, `selector2._domainkey` | The two targets shown for the domain in the **Microsoft Defender portal** under *Email authentication settings → DKIM* (search for "DKIM" there). After DNS updates, switch **Sign messages with DKIM** on there. |
| DMARC | TXT | `_dmarc.dixels.io` | Start with `v=DMARC1; p=none; rua=mailto:dmarc@dixels.io`. After a few weeks of clean reports, tighten to `p=quarantine`, then `p=reject`. |

---

## Diagnostics

The test endpoint and the log show the server's reply. **Retried** says whether a queued email that fails this way
is tried again automatically: temporary (4xx) failures are retried for up to 2 days with growing waits; permanent
(5xx) ones are logged as errors and dropped.

| Reply (exact text varies) | Meaning | Fix | Retried |
|---|---|---|---|
| `535 5.7.139 Authentication unsuccessful, SmtpClientAuthentication is disabled for the Mailbox` (or `…for the Tenant`) | SMTP AUTH is off for the mailbox or organisation. | Step 1.2. | No |
| `535 5.7.139 Authentication unsuccessful, basic authentication is disabled` | Basic sign-in used against Microsoft 365. | Set `ExchangeOnline.AuthType` to `OAuth2` (steps 2 to 5). | No |
| `535 5.7.139 Authentication unsuccessful, user is locked by your organization's security defaults policy` | Security Defaults are blocking SMTP AUTH. | Step 4. | No |
| `535 5.7.139 Authentication unsuccessful, the request did not meet the criteria to be authenticated successfully` | A Conditional Access policy blocked the sign-in. | Step 4: exclude or allow the app. | No |
| `535 5.7.3 Authentication unsuccessful` | Token accepted by Entra, but Exchange won't let the app into the mailbox: the service principal or mailbox permission is missing, the wrong Object ID was used, or changes are still applying. | Step 3. Wait up to an hour after changes. | No |
| `554 5.7.60 SMTP; Client does not have permissions to send as this sender` (SendAsDenied) | The From address isn't the service mailbox and Send As wasn't granted. The portal blocks this itself before sending, naming `ExchangeOnline.AllowedFromAddresses`. | Send from the service mailbox, or step 3.3 plus `AllowedFromAddresses`. | No |
| `432 4.3.2 STOREDRV.ClientSubmit; sender thread limit exceeded` | Too many connections at once from this mailbox. The portal keeps to 3 connections and 30 emails a minute, so this means another program is using the same mailbox too. | Give the other program its own mailbox. | Yes |
| `451 4.7.0 Temporary server error` / `421 4.3.2 Service not available` | Microsoft's side is busy. | None; it is retried. | Yes |
| `554 5.2.0 STOREDRV.Submission.Exception:SubmissionQuotaExceededException` | The mailbox reached its sending quota (10,000 recipients a day). | Wait 24 hours. If it is normal volume, ask Microsoft about higher-volume options. | No |
| `AADSTS7000215: Invalid client secret provided` | The secret in the settings is wrong. People often copy the secret's **ID** instead of its **Value**. | Step 2.3, then step 5. | No |
| `AADSTS7000222: The provided client secret keys … are expired` | The secret expired. | Create a new secret (step 2.3), then update step 5. | No |
| `AADSTS700016: Application with identifier … was not found` | Wrong client ID, or the app is in another tenant. | Check `ClientId` and `TenantId`. | No |
| `AADSTS90002: Tenant … not found` | Wrong tenant ID. | Check `TenantId`. | No |
| `The ExchangeOnline.… setting is not set` | A required setting is missing. The message names it. | Step 5. | No |
| Connection timed out / refused | Outbound port 587 to `smtp.office365.com` is blocked. | Allow outbound TCP 587 from the server. | Yes |
