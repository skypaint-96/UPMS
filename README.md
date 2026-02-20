# UPMS — Unified Problem Management System

A web-based system for problem managers who work across multiple ITSM sources. UPMS ingests snapshot exports from tools such as ServiceNow and Jira, stores ticket data as an append-only field-level change log, and provides point-in-time ticket reconstruction and a plugin-based report store.

---

## What It Does

- **Ingests snapshot exports** from one or more ITSM sources (CSV/JSON files uploaded via the web portal).
- **Stores ticket history** as field-level amendment records — one row per field value observed per ticket per snapshot. This allows any ticket to be reconstructed exactly as it appeared at any point in time.
- **Normalises field names** across ITSM sources using a canonical field mapping table, so that `incident_state` (ServiceNow) and `status` (Jira) are both stored and queried as `Status`.
- **Provides a web portal** (Blazor Server) for browsing snapshots, viewing tickets, uploading data, and running reports.
- **Hosts a reporting plugin system** — report generators are pluggable. The first planned plugins produce PowerPoint slide decks and HTML email notifications.

---

## Tech Stack

| Component | Technology |
|-----------|-----------|
| Web application | Blazor Server (.NET 10) |
| Data access library | C# class library (.NET 10), Dapper, Npgsql |
| Database | PostgreSQL 16 |
| Containerisation | Docker + Docker Compose |

There is no separate frontend SPA, no separate REST API, and no worker service. Blazor Server is the single deployable web component.

---

## Quick Start

**Prerequisites:** Docker Desktop (or Docker Engine with Compose v2+)

```bash
# Clone the repository
git clone <repository-url>
cd UPMS

# Start all services
docker compose up -d
```

The application will be available at **http://localhost:8080**.

To also start the database admin UI (Adminer):

```bash
docker compose --profile tools up -d
```

Adminer at **http://localhost:8090** — server: `db`, username: `upms`, password: `upms`, database: `upms`.

---

## Project Structure

```
UPMS/
├── src/
│   ├── UPMS.Data/          # Data access library — snapshots, field changes, point-in-time queries
│   └── UPMS.Web/           # Blazor Server web application — portal, upload, report store
│
├── tests/
│   ├── UPMS.Data.Tests/    # NUnit integration tests for the data layer
│   └── UPMS.Web.Tests/     # NUnit + Playwright end-to-end UI tests
│
├── sql/
│   ├── migrations/         # PostgreSQL schema migration scripts
│   └── stored-procedures/  # Stored procedures for point-in-time reconstruction
│
├── docker-compose.yml
├── docker-compose.override.yml
└── Docs/                   # Project documentation (see below)
```

---

## Reporting Plugins

The report store in `UPMS.Web` is built around an `IReportPlugin` interface. Plugins are discovered at startup via dependency injection. Each plugin declares its own parameter schema and generation logic, and can produce any output format (PPTX, HTML, PDF, etc.).

Planned plugins:
- **PowerPoint reporting pack** — templated slide decks with user-selectable parameters
- **Email notification** — pre-defined HTML email templates populated with ticket data

New report types can be added as plugins without modifying the core application.

> The reporting plugin system is **planned and not yet implemented**. See [`Docs/Build Stages.md`](Docs/Build%20Stages.md) for implementation status.

---

## Documentation

| Document | Description |
|----------|-------------|
| [`Docs/Project Overview.md`](Docs/Project%20Overview.md) | What UPMS is, core concepts, intended users |
| [`Docs/Technical Architecture.md`](Docs/Technical%20Architecture.md) | Project structure, database schema, data flow, plugin design |
| [`Docs/Build Stages.md`](Docs/Build%20Stages.md) | Implementation stages — what is complete and what is not yet started |
| [`Docs/Envionment.md`](Docs/Envionment.md) | Development environment specification |
| [`Docs/DOCKER.md`](Docs/DOCKER.md) | Docker Compose and Dockerfile details |
| [`Docs/UPMS_DATA_CONSUMER_GUIDE.md`](Docs/UPMS_DATA_CONSUMER_GUIDE.md) | Guide to using the `UPMS.Data` library |

---

## License

See [LICENSE](LICENSE) for details.
