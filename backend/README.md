# Dixels.Portal: backend

The API and sign-in server for the Dixels Portal: .NET 10, ABP Framework 10.6 (layered startup template),
EF Core on PostgreSQL, OpenIddict for sign-in. The React app in `../frontend` talks to it.

How the code is organised (ABP layers, where each kind of class lives): [docs/SOLID.md](../docs/SOLID.md).

> [!WARNING]
> **Running the Web app or the DbMigrator on this machine uses the live database.**
> The git-ignored `appsettings.secrets.json` files in `src/Dixels.Portal.Web` and `src/Dixels.Portal.DbMigrator`
> point at `DixelsPortal` on `localhost:5432`, which is the same database the live IIS site uses. That file is read
> *after* environment variables, so setting `ConnectionStrings__Default` in the environment does **not** move you to
> another database. To work against your own copy, put your own connection string in your own secrets file, or pass
> `--ConnectionStrings:Default="Host=...;Database=<your db>;..."` on the command line (command-line arguments win
> over the secrets file), and check that database exists first. Running the DbMigrator also re-seeds the sign-in
> clients' redirect addresses, so never run it against the live database with changed settings.
>
> To try the whole portal without touching anything shared, use Docker: [docs/docker.md](../docs/docker.md).

## Projects

| Project | What it is |
|---|---|
| `src/Dixels.Portal.Web` | The host you run: API, ABP's sign-in and admin pages, Swagger |
| `src/Dixels.Portal.DbMigrator` | Console app: applies migrations and seeds roles, permissions and sign-in clients |
| `src/Dixels.Portal.HttpApi` | The API controllers (`/api/app/...`) |
| `src/Dixels.Portal.Application(.Contracts)` | App services and DTOs |
| `src/Dixels.Portal.Domain(.Shared)` | Entities and business rules |
| `src/Dixels.Portal.EntityFrameworkCore` | `PortalDbContext`, mappings, migrations |
| `test/*` | xUnit tests; they use a throwaway SQLite database, so no PostgreSQL is needed |

## What you need

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet)
- PostgreSQL (to run the app; not needed for building or testing)
- Node.js and the ABP CLI (`dotnet tool install -g Volo.Abp.Studio.Cli`), only for `abp install-libs`

## Build and test

```bash
dotnet build Dixels.Portal.slnx -c Release
dotnet test Dixels.Portal.slnx -c Release
```

Use `-c Release`: a copy of the Web app is often running from Visual Studio and locks the Debug output folder.
The same commands run in CI (`.github/workflows/ci.yml`) on every pull request; they need no database, no secrets
file and no `wwwroot/libs`.

## Run it locally

1. **Client-side libraries** for ABP's pages (git-ignored, once per clone): in `src/Dixels.Portal.Web` run
   `abp install-libs`. It fills `wwwroot/libs`.
2. **Trust the development HTTPS certificate** (once per machine): `dotnet dev-certs https --trust`.
3. **Database.** Read the warning above first. For a database of your own, run the DbMigrator against it once:
   `dotnet run --project src/Dixels.Portal.DbMigrator -- --ConnectionStrings:Default="<your connection string>"`.
4. **Start the Web app**: `dotnet run --project src/Dixels.Portal.Web` (or F5 in Visual Studio).
   - App and sign-in pages: https://localhost:44393
   - Swagger: https://localhost:44393/swagger
5. Start the React app (see [../frontend/README.md](../frontend/README.md)); it runs at http://localhost:5173.

## Configuration

| Key | Where | Local value (in the committed `appsettings.json`) | What it does |
|---|---|---|---|
| `App:SelfUrl` | Web | `https://localhost:44393` | This server's own address; used for links to ABP's pages in emails |
| `App:ClientUrl` | Web | `http://localhost:5173` | The React app's address; used for "Open the portal" links in emails |
| `App:CorsOrigins` | Web | `http://localhost:5173,https://localhost:5173` | Comma-separated browser origins allowed to call the API |
| `ConnectionStrings:Default` | Web, DbMigrator | local PostgreSQL | The database. Overridden by `appsettings.secrets.json` |
| `OpenIddict:Applications:*:RootUrl` | DbMigrator | the server and localhost addresses | Seeded into the sign-in clients' allowed redirect addresses. Comma-separated |
| `Settings:Abp.Mailing.*` | Web (secrets file) | not set | ABP's email settings. See "Email" below |

`appsettings.secrets.json` (git-ignored) is read last and overrides everything above. There is deliberately **no
`appsettings.Production.json`**: the server keeps its own `appsettings.json` and `appsettings.secrets.json` in its
publish folder, and deploys never copy appsettings files over them. A Production file in the repo would either
never reach the server or, worse, be picked up by the DbMigrator (which runs as Production by default) and change
the seeded sign-in addresses. The server's values are listed in [docs/DEPLOYMENT.md](../docs/DEPLOYMENT.md).

## Email (smtp4dev locally)

The portal sends email with ABP's built-in sender, set through ABP's `Abp.Mailing.*` settings. Locally, use
**smtp4dev**, a fake mailbox that catches every email:

```bash
dotnet tool install -g Rnwood.Smtp4dev     # once
smtp4dev --urls http://localhost:5000 --smtpport 1025
```

Point ABP at it in `src/Dixels.Portal.Web/appsettings.secrets.json` (Host `127.0.0.1`, Port `1025`, EnableSsl
`false`); the full snippet is in [docs/docker.md](../docs/docker.md#without-docker-smtp4dev-on-its-own). Emails
then appear at http://localhost:5000. Real Microsoft 365 sending is in [docs/DEPLOYMENT.md](../docs/DEPLOYMENT.md).

## Token-signing certificate

Outside Development, OpenIddict signs tokens with `openiddict.pfx` from the Web app's folder (git-ignored; the
server keeps its own). To make a new one:

```bash
dotnet dev-certs https -v -ep openiddict.pfx -p <certificate password>
```

`<certificate password>` must be the password the Web app opens the file with (see
`AddProductionEncryptionAndSigningCertificate` in `PortalWebModule.cs`). More in ABP's
[Configuring OpenIddict](https://abp.io/docs/latest/deployment/configuring-openiddict#production-environment).

## Deploying

Deploying to the shared IIS server, and its configuration: [docs/DEPLOYMENT.md](../docs/DEPLOYMENT.md).
