# Dixels.Portal — Back-end

ABP Framework layered application (.NET 10, EF Core, LeptonX Lite MVC UI + Swagger).

## Run it

```bash
cd src/Dixels.Portal.DbMigrator
dotnet run          # first time only: creates the DB and seeds initial data

cd ../Dixels.Portal.Web
dotnet run
```

- App/UI: https://localhost:44393
- Swagger: https://localhost:44393/swagger
- Demo API used by the React frontend: `GET /api/demo/ping` (anonymous)

## Frontend connectivity

CORS is enabled for the React dev server via `App:CorsOrigins` in `src/Dixels.Portal.Web/appsettings.json`
(defaults to `http://localhost:5173` / `https://localhost:5173`). Add more origins there (comma-separated)
if the frontend runs elsewhere.

The dev HTTPS certificate needs to be trusted once per machine:

```bash
dotnet dev-certs https --trust
```
