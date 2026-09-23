# Dixels.Portal — Front-end

React + TypeScript SPA, scaffolded with Vite.

## Run it

```bash
npm install
npm run dev
```

- App: http://localhost:5173

## Backend connectivity

The API base URL is read from `VITE_API_URL` (see `.env`), defaulting to the ABP backend at
`https://localhost:44393`. `src/api.ts` calls the backend's demo endpoint (`GET /api/demo/ping`),
and `App.tsx` shows the result on load as a connectivity check.

Make sure the backend is running and its dev HTTPS certificate is trusted
(`dotnet dev-certs https --trust`), otherwise the browser will block the request.
