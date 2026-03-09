# UPMS — Unified Problem Management System

UPMS is a historical problem-management platform for teams working across multiple ITSM sources. It ingests exported ticket snapshots, stores field-level change history in PostgreSQL, reconstructs ticket state at any chosen time, and generates reporting outputs from a reusable plugin model.

This repository now contains both:

- the **legacy Blazor Server application** (`src/UPMS.Web`) retained as a transition/reference implementation
- the **new split architecture** built around a `.NET 10` API, `.NET 10` worker, and **React 19 + CGI EDS** frontend

## New target architecture

| Component | Technology | Purpose |
|----------|------------|---------|
| `UPMS.Api` | ASP.NET Core Minimal API (`.NET 10`) | primary application boundary for frontend, worker, and external consumers |
| `UPMS.Worker` | Background worker (`.NET 10`) | queued snapshot ingest and report execution |
| `UPMS.Frontend` | React 19 + CGI EDS + MUI 5 | primary browser UI |
| `UPMS.Data` | `.NET 10` class library | shared persistence, reconstruction logic, job queue, artifact storage |
| `UPMS.Ingestion` | `.NET 10` class library | reusable CSV/JSON ingest orchestration |
| `UPMS.Reporting` | `.NET 10` class library | reusable report plugins and template/report services |
| `UPMS.Web` | Blazor Server (`.NET 10`) | legacy/reference application retained during transition |

## What UPMS does

- ingests CSV/JSON snapshot exports from multiple ITSM sources
- stores append-only ticket field history
- reconstructs point-in-time ticket state
- normalises source fields to canonical fields
- exposes reporting and document-generation plugins
- now supports API-first access and worker-backed jobs

## Runtime defaults

The default container runtime is now:

- `UPMS_db` — PostgreSQL 16
- `UPMS_api` — `.NET 10` API
- `UPMS_worker` — `.NET 10` worker
- `UPMS_frontend` — React/CGI EDS frontend served by Nginx

## Quick start (containers)

```bash
docker compose up --build -d
```

For a clean rebuild on a machine that already has UPMS containers or networks:

```bash
docker compose down --remove-orphans -v
docker compose build --no-cache
docker compose up -d
```

Default ports:

- Frontend: `http://localhost:8080`
- API: `http://localhost:8081`
- PostgreSQL: `localhost:5432`

## Frontend package constraints

The React frontend is aligned to the supplied CGI EDS package constraints:

- React `19.0.0`
- MUI `5.15.14`
- Node `20.x`
- Yarn `1.22.19`

## Repository layout

```text
src/
  UPMS.Api/
  UPMS.Worker/
  UPMS.Frontend/
  UPMS.Data/
  UPMS.Ingestion/
  UPMS.Reporting/
  UPMS.Web/

tests/
  UPMS.Api.Tests/
  UPMS.Data.Tests/
  UPMS.Web.Tests/

Docs/
  Modernisation/
```

## Key documentation

- `Docs/Modernisation/00-Current-Solution-Review.md`
- `Docs/Modernisation/10-Architecture-Blueprint.md`
- `Docs/Modernisation/20-API-Core-Requirements.md`
- `Docs/Modernisation/21-API-Core-Tests.md`
- `Docs/Modernisation/22-API-Core-Implementation.md`
- `Docs/Modernisation/30-Worker-Requirements.md`
- `Docs/Modernisation/32-Worker-Implementation.md`
- `Docs/Modernisation/40-React-Frontend-Requirements.md`
- `Docs/Modernisation/42-React-Frontend-Implementation.md`
- `Docs/Modernisation/50-Container-Hosting.md`
- `Docs/Modernisation/Examples/PowerQuerySample.m`

## Notes

The container environment used to prepare this package did not include the `.NET SDK`, so the new `.NET 10` projects and tests were added as source and project files without a local compile/run pass in this environment. The frontend package and Docker assets were also prepared as source-level deliverables.


## Troubleshooting

- The frontend Dockerfile now installs `yarn@1.22.19` with `--force` because the `node:20-alpine` base image already contains a `yarn` shim, which otherwise causes `EEXIST` during image build.
- If `docker compose down -v` reports that `upms_default` is still in use, re-run with `--remove-orphans` and remove any stale containers still attached to that network before retrying.

## Known packaging fixes applied

This package includes two important fixes on top of the original modernization scaffold:

1. **`UPMS.Data/Artifacts` is explicitly un-ignored in `.gitignore`** so the source folder is preserved on Windows case-insensitive working trees.
2. **`eds-react-app` stylesheet vendoring for Vite builds**. The CGI EDS tarball contains `dist/style.css`, but its package `exports` map does not expose that subpath. The frontend now imports a local vendored copy at `src/styles/eds-react-app.css` so `vite build` succeeds without modifying the upstream package tarball.

The more permanent upstream library fix would be to add `./dist/style.css` to the `exports` section of the CGI EDS package.
