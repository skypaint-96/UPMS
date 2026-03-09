# UPMS — Unified Problem Management System

UPMS is a historical problem-management platform for teams working across multiple ITSM sources. It ingests exported ticket snapshots, stores field-level change history in PostgreSQL, reconstructs ticket state at any chosen time, and generates reporting outputs from a reusable template-first reporting system.

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
- stores a shared report-template library for generated documents, emails, spreadsheets, payloads, and slide decks
- seeds example starter templates for each supported template type on fresh state
- supports extensible template type registration for new upload/rendering formats
- now supports API-first access and worker-backed jobs

## Template-first reporting

The modern API/worker reporting path is now centered on the tokenised template runner rather than a catalogue of hard-coded report definitions.

- Supported template types are registered via `IReportTemplateTypeProvider`, so new formats can be added in code without redesigning the template library UX.
- Fresh installs seed starter examples from `src/UPMS.Reporting/StarterTemplates/` into the configured report-template storage on API and worker startup.
- The React **Report Templates** workspace supports upload, inline editing for text-like formats, metadata updates, export/download, and deletion from one shared library.
- The React **Reports** page generates directly from that template library, so day-to-day report execution revolves around choosing a template and supplying parameters.

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

## Frontend navigation model

The React UI now separates regular and power-user workflows:

- **Sidebar**: dashboard, tickets, and template-driven report generation for day-to-day users
- **Top-right settings/cog**: ITSM source administration, snapshot upload/browsing, jobs, and the shared report-template library

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

## Notes from the latest patch

- Reporting in the API/worker architecture is now template-first: the concrete example report registrations were removed in favour of the shared `tokenised-template-report` runner.
- Supported template types are now managed through an extensible registry, with fresh-state starter templates seeded automatically for HTML, EML, TXT, CSV, XML, DOCX, XLSX, and PPTX.
- The React template workspace now supports type-aware upload, inline editing for text-like templates, export/download, deletion, and direct guidance for starter examples and token syntax.
- The worker container now starts via the published application host (`./UPMS.Worker`) rather than invoking `dotnet` directly at container entrypoint time.
- Frontend data tables now explicitly disable the EDS `displaySelected` mode so rows are visible by default.
- ITSM source import now accepts a broader set of legacy JSON and CSV field names, and canonical aliases such as `ticket_key`, `company`, `title`, and `status` are normalized to the current registered canonical fields.
- Sample ITSM source definitions and matching snapshot CSV files are available under `Docs/Modernisation/SampleItsmSources/`.
