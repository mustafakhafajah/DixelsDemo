# Running the portal in Docker

One command starts the whole portal on your machine: the database, the API (with the sign-in pages) and the web app.
It does not touch the shared IIS server or the Postgres already installed on this machine.

| What | Address |
|---|---|
| Web app | http://localhost:8080 |
| API, sign-in pages, Swagger | https://localhost:8443 (Swagger at `/swagger`) |
| Database (for pgAdmin etc.) | `localhost:5433`, user `postgres`, password `docker-local-only`, database `DixelsPortal` |

> The Docker database has its own password (`docker-local-only`), separate from any real one. A database volume
> made before 7 Oct 2026 still has the old password: run `docker compose down -v` once to start it fresh.

| Mailbox (smtp4dev) | http://localhost:5000: every email the portal sends lands here, nothing reaches real inboxes |

Sign in with the default ABP admin: `admin` / `1q2w3E*`.

## One-time setup

1. **Docker Desktop** must be running. On Windows it needs WSL 2, which needs two things:
   - **Virtualization turned on in the PC's firmware.** Restart into the BIOS/UEFI setup (usually F2, F10 or Del
     during start-up) and enable *Intel VT-x* / *Virtualization Technology* or *AMD SVM*. Task Manager → Performance →
     CPU shows "Virtualization: Enabled" once it's on.
   - **WSL installed.** In an admin terminal, run `wsl --install`, then restart.
2. **HTTPS certificate for the API.** The API container uses your machine's ASP.NET Core development certificate, the
   same one your browser already trusts for `https://localhost:44393`. Export it into `docker/certs` (git ignores it):

   ```
   dotnet dev-certs https --trust
   dotnet dev-certs https -ep docker/certs/localhost.pfx -p dixels-dev
   ```

   To use another password, also put `DEV_CERT_PASSWORD=<it>` in a `.env` file next to `docker-compose.yml`.

## Everyday use

From the repo root:

```
docker compose up --build        # build and start everything (add -d to run in the background)
docker compose down              # stop; the database is kept
docker compose down -v           # stop and wipe the database
docker compose logs -f api       # follow the API's log
```

On every `up`, the `migrator` service runs first. It applies new database migrations and seeds the admin user,
permissions and sign-in clients, then exits. The API starts only after it finishes successfully.

## How it fits together

- `backend/Dockerfile` builds two images from the same source: `web` (Dixels.Portal.Web) and `migrator`
  (Dixels.Portal.DbMigrator). During the build it also fills `wwwroot/libs` with `abp install-libs` and creates a
  token-signing certificate (`openiddict.pfx`) for the image.
- `frontend/Dockerfile` builds the React app with the Docker addresses (`VITE_API_URL=https://localhost:8443`,
  served at `/`) and serves it with nginx.
- Settings come from environment variables in `docker-compose.yml`, not from the git-ignored
  `appsettings.secrets.json` files, so nothing machine-specific ends up in an image.
- Sign-in cookie keys are kept in a Docker volume, so you stay signed in after a restart. Rebuilding the API image
  makes a new token-signing certificate, so you may need to sign in again after a rebuild.
- Email: ABP's own email sender, set through ABP's `Abp.Mailing.*` settings, sends to the `smtp4dev` container
  (plain SMTP on port 25, no sign-in) instead of Microsoft 365. To try it, book a room in the web app, or use ABP's
  built-in test: sign in to Swagger as admin and call `POST /api/setting-management/emailing/send-test-email` with
  `{ "senderEmailAddress": "noreply@dixels.local", "targetEmailAddress": "anyone@example.com", "subject": "Test", "body": "Hello" }`.
  Then open http://localhost:5000. Real Microsoft 365 sending is in [DEPLOYMENT.md](DEPLOYMENT.md).

## Without Docker: smtp4dev on its own

To try emails with the portal running from Visual Studio, run smtp4dev directly:

```
dotnet tool install -g Rnwood.Smtp4dev          # once
smtp4dev --urls http://localhost:5000 --smtpport 1025
```

Then put ABP's mail settings in the git-ignored `backend/src/Dixels.Portal.Web/appsettings.secrets.json`:

```json
"Settings": {
  "Abp.Mailing.Smtp.Host": "127.0.0.1",
  "Abp.Mailing.Smtp.Port": "1025",
  "Abp.Mailing.Smtp.EnableSsl": "false",
  "Abp.Mailing.DefaultFromAddress": "noreply@dixels.local",
  "Abp.Mailing.DefaultFromDisplayName": "Dixels Portal"
}
```

## Troubleshooting

- **The browser warns about the certificate on https://localhost:8443**: run step 2 again. The dev certificate
  renews once a year, and the copy in `docker/certs` must be exported again after that.
- **Port already in use**: change the left-hand port numbers in `docker-compose.yml`. Changing 8080 or 8443 also
  means changing the matching addresses in that file (`RootUrl`, `SelfUrl`, `CorsOrigins`, `VITE_API_URL`). Then run
  `docker compose down -v` so the sign-in clients are seeded again with the new addresses.
