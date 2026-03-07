# UPMS — Unified Problem Management System

A web-based system for problem managers who work across multiple ITSM sources. UPMS ingests snapshot exports from tools such as ServiceNow and Jira, stores ticket data as an append-only field-level change log, and provides point-in-time ticket reconstruction and a plugin-based report store.

---

## What It Does

- **Ingests snapshot exports** from one or more ITSM sources (CSV/JSON files uploaded via the web portal).
- **Stores ticket history** as field-level amendment records — one row per field value observed per ticket per snapshot. This allows any ticket to be reconstructed exactly as it appeared at any point in time.
- **Normalises field names** across ITSM sources using a canonical field mapping table. The current canonical set uses human-friendly names such as `Number`, `Company`, `State`, `Assigned To`, `Assignment Group`, `Opened At`, `Updated On`, and `Resolved At` (legacy aliases like `ticket_number` and `status` are still recognised for compatibility).
- **Provides a web portal** (Blazor Server) for browsing snapshots, viewing tickets, uploading data, and running reports.
- **Hosts a reporting plugin system** — report generators are pluggable and can return HTML previews or downloadable files (PPTX/CSV/etc.).

---

## Tech Stack

| Component | Technology |
|-----------|-----------|
| Web application | Blazor Server (.NET 10) |
| Data access library | C# class library (.NET 10), EF Core + Npgsql |
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

The application will be available at **http://localhost:8081**.

Optional: a DB admin UI (e.g., Adminer) is not included by default in the compose file, but you can add it under a `tools` profile. See [`Docs/DOCKER.md`](Docs/DOCKER.md).

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
│   └── stored-procedures/  # Reference SQL for point-in-time reconstruction (also deployed via EF migrations)
│
├── docker-compose.yml
├── docker-compose.override.yml
└── Docs/                   # Project documentation (see below)
```

---

## Reporting Plugins

The report store in `UPMS.Web` is built around an `IReportPlugin` interface. Plugins are discovered at startup via dependency injection. Each plugin declares its own parameter schema and generation logic, and can produce any output format (HTML previews, PPTX/CSV downloads, etc.).

Included plugins:
- **PowerPoint Report Pack** — downloads a simple PPTX summarising ticket data.
- **Email Notification** — renders an HTML email preview or downloads a ready-to-send `.eml` draft.
- **Month End Lifecycle Report (Example)** — HTML month-end report with last-12-month lifecycle graphs and configurable detail fields.
- **Ticket Document Export** — exports a single ticket or a selected ticket set as DOCX or PDF.
- **Tokenised Template Fill** — fills uploaded email/document/spreadsheet templates with `{{token}}` placeholders.
- **Status Breakdown (Example)** — HTML breakdown of ticket counts by State (or another field).
- **Ticket CSV Export (Example)** — downloads tickets as a CSV file.
- **Field Delta (Example)** — compares ticket field values between two dates (new/removed/changed).

Template uploads are available under `/report-templates`. Developer notes / contract documentation: see [`Docs/Reporting Plugins.md`](Docs/Reporting%20Plugins.md).

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
