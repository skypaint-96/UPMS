# UPMS — Unified Problem Management System

[![Prod](https://github.com/skypaint-96/UPMS/actions/workflows/ci-cd-upms.yml/badge.svg?branch=Production)](https://github.com/skypaint-96/UPMS/actions/workflows/ci-cd-upms.yml) [![Dev](https://github.com/skypaint-96/UPMS/actions/workflows/ci-cd-upms.yml/badge.svg?branch=Development)](https://github.com/skypaint-96/UPMS/actions/workflows/ci-cd-upms.yml)

UPMS is a historical problem-management platform for teams working across multiple ITSM sources. It ingests exported ticket snapshots, stores field-level change history in PostgreSQL, reconstructs ticket state at any chosen time, and generates reporting outputs from a reusable template-first reporting system.

This repository now contains only the active runtime stack: API, worker, React frontend, and shared domain libraries. The earlier transition-era Blazor monolith has been removed to reduce duplication and keep the repository aligned with what is actually built and shipped.

## Active architecture

| Component | Technology | Purpose |
|----------|------------|---------|
| `UPMS.Api` | ASP.NET Core Minimal API (`.NET 10`) | primary application boundary for frontend, worker, and external consumers |
| `UPMS.Worker` | Background worker (`.NET 10`) | queued snapshot ingest and report execution |
| `UPMS.Frontend` | React 19 + CGI EDS + MUI 5 | browser UI |
| `UPMS.Data` | `.NET 10` class library | persistence, reconstruction logic, job queue, artifact storage |
| `UPMS.Ingestion` | `.NET 10` class library | reusable CSV/JSON ingest orchestration |
| `UPMS.Reporting` | `.NET 10` class library | report plugins, template library, template rendering |

## What UPMS does

- ingests CSV and JSON snapshot exports from multiple ITSM sources
- stores append-only ticket field history
- reconstructs point-in-time ticket state
- normalises source fields to canonical fields, while still accepting legacy aliases during import
- stores a shared report-template library for generated documents, emails, spreadsheets, payloads, and slide decks
- seeds starter templates for each supported template type on fresh state
- exposes API-first access for frontend and external consumers
- runs long-running ingest and reporting work through a background worker

## Template-first reporting

The reporting path is centered on the tokenised template runner rather than a catalogue of hard-coded report definitions.

- Supported template types are registered via `IReportTemplateTypeProvider`, so new formats can be added in code without redesigning the template-library UI.
- Fresh installs seed starter examples from `src/UPMS.Reporting/StarterTemplates/` into the configured report-template storage on API and worker startup.
- The React **Report Templates** workspace supports upload, inline editing for text-like formats, metadata updates, export/download, and deletion from one shared library.
- The React **Reports** page generates directly from that template library, so report execution revolves around choosing a template and supplying parameters.

## Quick start

```bash
docker compose up --build -d
```

For a clean rebuild:

```bash
docker compose down --remove-orphans -v
docker compose build --no-cache
docker compose up -d
```

Default local ports:

- Frontend: `http://localhost:8080`
- API: `http://localhost:8081`
- PostgreSQL: `localhost:5432`

Compose defaults are intentionally usable for local development:

- `UPMS_DB_PASSWORD` defaults to `upms`
- `UPMS_API_KEY` defaults to `change-me`
- `docker-compose.override.yml` switches the API to development mode and disables auth for local work

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

tests/
  UPMS.Api.Tests/
  UPMS.Data.Tests/

Docs/
  Modernisation/
```

## Key documentation

- `Docs/DOCKER.md`
- `Docs/ITSM_SOURCE_DEFINITION.md`
- `Docs/UPMS_DATA_CONSUMER_GUIDE.md`
- `Docs/STARTER_REPORT_TEMPLATES.md`
- `Docs/Reporting Plugins.md`
- `Docs/Modernisation/10-Architecture-Blueprint.md`
- `Docs/Modernisation/20-API-Core-Requirements.md`
- `Docs/Modernisation/22-API-Core-Implementation.md`
- `Docs/Modernisation/30-Worker-Requirements.md`
- `Docs/Modernisation/32-Worker-Implementation.md`
- `Docs/Modernisation/40-React-Frontend-Requirements.md`
- `Docs/Modernisation/42-React-Frontend-Implementation.md`
- `Docs/Modernisation/50-Container-Hosting.md`

## Notes

- The frontend depends on the vendored `vendor/eds-react-app-v17.0.0.tgz` package.
- The frontend also vendors `src/UPMS.Frontend/src/styles/eds-react-app.css` because the supplied EDS tarball does not expose that stylesheet path for Vite builds.
