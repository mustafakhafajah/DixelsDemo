# Dixels.Portal: frontend

The Dixels Portal web app: React 19, TypeScript and Vite, with TanStack Query for data, react-oidc-context for
sign-in, i18next for English and Arabic, and Tailwind CSS. It talks to the ABP backend in `../backend`.

## What you need

- Node.js 24 (LTS) and npm
- The backend running somewhere the browser can reach (see below)

## Run it locally

```bash
npm ci          # install exactly what package-lock.json lists
npm run dev     # http://localhost:5173
```

The app signs in through the backend and calls its API at `VITE_API_URL`, which `.env` sets to
`https://localhost:44393`, the backend started from Visual Studio or `dotnet run`. Trust the backend's development
certificate once (`dotnet dev-certs https --trust`), or the browser blocks the requests.

> [!WARNING]
> **A locally run backend uses the live database.** Its git-ignored `appsettings.secrets.json` points at the same
> PostgreSQL database the live site uses, so bookings you make locally are real. See
> [../backend/README.md](../backend/README.md) for how to point it at your own database, or run everything in Docker
> ([../docs/docker.md](../docs/docker.md)).

## Scripts

| Command | What it does |
|---|---|
| `npm run dev` | Development server with hot reload |
| `npm run lint` | Oxlint (`.oxlintrc.json`) |
| `npm test` | Vitest, once |
| `npm run build` | Type-check (`tsc -b`), then build into `dist/` using `.env.production` |
| `npm run preview` | Serve the built `dist/` locally |

CI (`.github/workflows/ci.yml`) runs `npm ci`, lint, test and build on every pull request.

## Settings

All settings are `VITE_` variables, explained in [.env.example](.env.example):

| Variable | Development (`.env`) | Shared server (`.env.production`) |
|---|---|---|
| `VITE_API_URL` | `https://localhost:44393` | `https://192.168.2.164/server` |
| `VITE_BASE` | `/` (default) | `/portal/` |

They are baked into the build and visible to anyone, so they hold addresses only, never secrets. For a setting just
for your machine, create `.env.local` (git-ignored).

## Email while testing

Booking and welcome emails are sent by the backend. Locally, catch them with smtp4dev
(http://localhost:5000); setup is in [../backend/README.md](../backend/README.md#email-smtp4dev-locally).

## Deploying

The shared IIS server serves the app under `/portal/`. `npm run build` produces `dist/`, including
`public/web.config`, which sends every unknown path to `/portal/index.html` so deep links work. Steps:
[../docs/DEPLOYMENT.md](../docs/DEPLOYMENT.md).
