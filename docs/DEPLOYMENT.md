# Deploying to the shared server

The portal runs on one Windows PC with IIS, at **https://192.168.2.164**:

| Part | Address | Live folder |
|---|---|---|
| React app | https://192.168.2.164/portal | `C:\Publish\DixelsPortalWeb` |
| API, sign-in pages, Swagger (`Dixels.Portal.Web`) | https://192.168.2.164/server | `C:\Publish\DixelsPortalDemo` |

Deploy from `main` only, after the pull request is merged. The server keeps its **own configuration** in the live
folder, so a deploy never copies these over it: `appsettings*.json`, `openiddict.pfx`, `web.config`, `App_Data`
(sign-in cookie keys and profile pictures) and `Logs`.

### Server configuration (kept on the server, not in git)

| File in `C:\Publish\DixelsPortalDemo` | What it holds |
|---|---|
| `appsettings.json` | `App:SelfUrl` = `https://192.168.2.164/server`, `App:ClientUrl` = `https://192.168.2.164/portal`, `App:CorsOrigins` = `https://192.168.2.164` (plus the localhost dev origins) |
| `appsettings.secrets.json` | The connection string and the `Abp.Mailing.*` settings (see the email part below) |
| `openiddict.pfx` | The token-signing certificate |
| `web.config` | IIS hosting settings for the app |

The React app's server addresses are committed in `frontend/.env.production` (`VITE_API_URL`, `VITE_BASE`); each
variable is explained in `frontend/.env.example`.

### Steps

1. **Back up** both live folders to `C:\Publish\_backup\<date>\`. Rolling back = copying them back.
2. **Backend:**
   1. `dotnet publish backend/src/Dixels.Portal.Web -c Release -o <staging folder>`.
   2. Put an `app_offline.htm` in `C:\Publish\DixelsPortalDemo` (IIS stops the app and shows that page).
   3. Copy without the server's own files:
      `robocopy <staging folder> C:\Publish\DixelsPortalDemo /E /XF appsettings*.json openiddict.pfx web.config /XD App_Data Logs`
      (robocopy exit codes 1 to 3 mean success).
   4. If the change has a database migration or new permissions, run the DbMigrator now (next step), then remove
      `app_offline.htm`.
3. **Database (only when needed):** build `backend/src/Dixels.Portal.DbMigrator` in Release and run the exe from
   its `bin\Release\net10.0` folder. Its git-ignored `appsettings.secrets.json` points at the live database. It also
   re-seeds the sign-in clients' redirect addresses from its `appsettings.json` (`OpenIddict:Applications`), so
   those addresses must stay exactly as they are or sign-in breaks.
4. **Frontend:** in `frontend`, `npm ci` then `npm run build` (uses `.env.production`), then
   `robocopy dist C:\Publish\DixelsPortalWeb /MIR`. `dist` includes `web.config`, which sends deep links to
   `/portal/index.html`.
5. Open https://192.168.2.164/portal, sign in and check a page that lists data.

> Don't publish straight into the live folder from Visual Studio (a Folder publish profile pointing at
> `C:\Publish\DixelsPortalDemo`): it would copy the repo's `appsettings.json` and `web.config` over the server's.

---

# Email through Microsoft 365: setup checklist

The portal sends its emails (booking confirmations, changes, cancellations, reminders, welcome emails, ABP's
password resets) with **ABP's built-in email sender** (MailKit). It is set entirely through ABP's own
`Abp.Mailing.*` settings, and it signs in to the mail server with a **mailbox user name and password**.

> **Microsoft is retiring password sign-in for SMTP.** Exchange Online turns it off by default at the end of
> December 2026. A Microsoft 365 admin can turn it back on for the organisation until Microsoft's final cut-off,
> which Microsoft will announce in 2027. After that, ABP's sender can no longer sign in to Microsoft 365.

You need: a Microsoft 365 admin, access to the sending domain's DNS, and an admin account in the portal.

---

## 1. The sending mailbox (Microsoft 365 admin)

1. Create a user mailbox, e.g. `noreply@dixels.io`, with a strong password. It must be able to sign in, so a shared
   mailbox with sign-in blocked won't work. It needs an Exchange Online licence.
2. Exclude it from MFA. Password SMTP sign-in can't answer an MFA prompt. If **Security Defaults** are on, they block
   it outright; then use a Conditional Access policy that excludes this one account instead.
3. Turn on **SMTP AUTH** for this mailbox only (Exchange Online PowerShell):

   ```powershell
   Connect-ExchangeOnline
   Set-CASMailbox -Identity noreply@dixels.io -SmtpClientAuthenticationDisabled $false
   Get-CASMailbox -Identity noreply@dixels.io | Format-List SmtpClientAuthenticationDisabled   # should say False
   ```

## 2. Server settings

**`C:\Publish\DixelsPortalDemo\appsettings.secrets.json`** (git-ignored; deploys leave it alone). Replace any older
email settings with ABP's:

```json
"Settings": {
  "Abp.Mailing.Smtp.Host": "smtp.office365.com",
  "Abp.Mailing.Smtp.Port": "587",
  "Abp.Mailing.Smtp.EnableSsl": "false",
  "Abp.Mailing.Smtp.UseDefaultCredentials": "false",
  "Abp.Mailing.Smtp.UserName": "noreply@dixels.io",
  "Abp.Mailing.DefaultFromAddress": "noreply@dixels.io",
  "Abp.Mailing.DefaultFromDisplayName": "Dixels Portal"
}
```

- `EnableSsl` stays `false`. ABP's MailKit sender then upgrades the connection with STARTTLS, which is what port 587
  uses; `true` would expect port 465.
- **The password doesn't go in the file.** It's an encrypted ABP setting. Sign in to the server's ABP pages as admin,
  open **Settings → Emailing**, and type the mailbox password there. ABP stores it encrypted in the database.
- **`C:\Publish\DixelsPortalDemo\appsettings.json`**, under `"App"`: set `"ClientUrl": "https://192.168.2.164/portal"`.
  Emails use it for their "View my bookings" / "Open the portal" buttons.
- **IIS:** set the site's app pool to **Start Mode = AlwaysRunning** and **Idle Time-out = 0**. Booking reminders are
  sent by the site itself, so they stop while the app pool sleeps. A reminder that comes due after its booking has
  started is dropped.
- Recycle the app pool after changing the files.

## 3. Check it works

Use ABP's built-in test:
1. On the server's **Settings → Emailing** page, click *Send test email*.
2. Or call `POST /api/setting-management/emailing/send-test-email` with
   `{ "senderEmailAddress": "noreply@dixels.io", "targetEmailAddress": "you@dixels.io", "subject": "Test", "body": "Hello" }`.

If it fails, the exact reason is in the site's log (`C:\Publish\DixelsPortalDemo\Logs\logs.txt`). The API itself
only answers "Mail sending failed". Then book a room in the portal: the confirmation should arrive within a minute.

## 4. DNS for the sending domain

Without these, emails land in spam or are rejected.

| Record | Type | Name | Value |
|---|---|---|---|
| SPF | TXT | `dixels.io` | `v=spf1 include:spf.protection.outlook.com -all`. Add the `include:` to an existing SPF record rather than creating a second one. |
| DKIM | CNAME ×2 | `selector1._domainkey`, `selector2._domainkey` | The two targets the Microsoft Defender portal shows under *Email authentication settings → DKIM*. Then switch DKIM signing on there. |
| DMARC | TXT | `_dmarc.dixels.io` | Start with `v=DMARC1; p=none; rua=mailto:dmarc@dixels.io`, then tighten to `quarantine` / `reject`. |

## How sending behaves

- Emails are queued as ABP background jobs, so they're sent within seconds of the action that caused them.
  ABP retries a failed email with growing waits for up to 2 days, whatever the reason.
- Exchange Online allows about 30 messages a minute and 10,000 recipients a day per mailbox. A large burst, such as
  cancelling many bookings at once, can hit the per-minute limit. Those emails then fail with a 4xx error, and ABP
  retries them shortly after.

## Common errors (in the log)

| Error | Meaning | Fix |
|---|---|---|
| `535 5.7.139 Authentication unsuccessful, SmtpClientAuthentication is disabled …` | SMTP AUTH is off for the mailbox or organisation. | Step 1.3. |
| `535 5.7.139 … basic authentication is disabled` / `… security defaults policy` | Microsoft or Security Defaults are blocking password sign-in. | Step 1.2, or have the admin re-enable SMTP password sign-in. After Microsoft's final cut-off this can't be fixed. |
| `535 5.7.3 Authentication unsuccessful` | Wrong user name or password. | Retype the password on Settings → Emailing. |
| `554 5.7.60 … not have permissions to send as this sender` | `DefaultFromAddress` isn't the mailbox. | Make `DefaultFromAddress` the same as `UserName`. |
| `432 4.3.2 STOREDRV.ClientSubmit; sender thread limit exceeded` | Too many emails or connections at once. | None: ABP retries. |
| `554 5.2.0 … SubmissionQuotaExceededException` | The daily sending limit has been reached. | Wait 24 hours. |
| Connection timed out or refused | Outbound port 587 is blocked. | Allow outbound TCP 587 to `smtp.office365.com`. |

## Testing without Microsoft 365

Point the same settings at **smtp4dev**, a fake mailbox: Host `127.0.0.1`, Port `1025`, EnableSsl `false`, no
user name, `UseDefaultCredentials` left at ABP's default. Every email then lands at http://localhost:5000 instead of a
real inbox. See [docker.md](docker.md).
