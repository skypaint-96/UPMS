# Technical Architecture

This document describes how UPMS is structured and how its main features are implemented.

---

## Project Structure

UPMS is a small solution with two main runtime projects and two test projects:

| Project | Type | Purpose |
|---------|------|---------|
| `src/UPMS.Data/` | Class library (.NET 10) | Data model + data access (EF Core), ticket reconstruction logic, ITSM source + field mapping services |
| `src/UPMS.Web/` | Blazor Server app (.NET 10) | Web UI (upload, browsing, reporting), ingest orchestration, report store + plugin host |
| `tests/UPMS.Data.Tests/` | NUnit test project | Data-layer tests (uses SQLite in-memory) |
| `tests/UPMS.Web.Tests/` | NUnit + Playwright | UI / end-to-end tests |

There is:

- no separate REST API backend
- no SPA frontend
- no worker service

The Blazor Server application is the single deployable web component.

---

## High-Level Architecture

### Web application (`UPMS.Web`)

`UPMS.Web` is a Blazor Server application (Interactive Server). The browser maintains a SignalR connection; UI events execute on the server.

Key pages:

| Page | Route | Purpose |
|------|-------|---------|
| `Home.razor` | `/` | Landing / navigation |
| `Snapshots.razor` | `/snapshots` | Browse uploaded snapshots |
| `SnapshotDetail.razor` | `/snapshots/{id}` | Inspect a snapshot and click through to ticket views “as-of” that snapshot |
| `Tickets.razor` | `/tickets` | Search tickets as-of a chosen time |
| `TicketDetail.razor` | `/tickets/{company}/{ticketKey}` and `/tickets/{company}/{itsmSource}/{ticketKey}` | Reconstruct a ticket “as-of” a chosen time and display field history |
| `Upload.razor` | `/upload` | Upload a CSV snapshot |
| `ItsmSources.razor` | `/itsm-sources` | Manage ITSM sources |
| `ItsmSourceDetail.razor` | `/itsm-sources/{name}` | Manage field mappings for a specific ITSM source |
| `ReportStore.razor` | `/reports` | Run reporting plugins |

Point-in-time navigation:

- `Tickets.razor` includes an **As Of** input and passes `?asOf=...` when linking to ticket detail.
- `SnapshotDetail.razor` links to ticket detail with `?asOf=<snapshot_date>` (so the ticket detail view matches what you clicked on).

### Data layer (`UPMS.Data`)

`UPMS.Data` uses EF Core with the Npgsql provider (PostgreSQL).

Important services:

- `TicketDataService` – reads/writes snapshots, snapshot rosters, and the append-only field change log.
- `TicketDataServiceInstance` – DI wrapper around `TicketDataService` (scoped).
- `IItsmSourceService` / `ItsmSourceService` – CRUD for ITSM source definitions.
- `IItsmFieldMappingService` / `ItsmFieldMappingService` – CRUD for source→canonical field mappings.

---

## Core Data Model

UPMS stores snapshot data in an **append-only change log**. This enables exact reconstruction of ticket state at any point in time.

### `raw_snapshot`

One row per uploaded snapshot.

Key columns:

- `id` (Guid)
- `itsm_source` (string)
- `snapshot_date` (DateTime)
- `uploaded_by` (string)
- `uploaded_at` (DateTime)

### `snapshot_ticket`

One row per ticket per snapshot (“roster”). This is used to quickly answer:

- “Which tickets existed in snapshot X?”
- “Which tickets existed for company Y as-of time T?”

Key columns:

- `snapshot_id` (Guid)
- `company_name` (string)
- `ticket_key` (string)

### `field_change`

Append-only history table: one row per observed field value per ticket per snapshot.

Key columns:

- `company_name` (string)
- `ticket_key` (string)
- `field_name` (string) – canonical name (preferred) or raw source name
- `field_value` (nullable string)
- `observed_at` (DateTime)
- `snapshot_id` (Guid)

### Point-in-time reconstruction

To reconstruct a ticket at time `T`:

1. Find the latest snapshot for that ticket with `snapshot_date <= T`
2. For that snapshot, for each `field_name`, take the latest observed value with `observed_at <= snapshot_date`

In code, this is implemented in `TicketDataService` using EF queries.

---

## ITSM Source & Field Mapping

### `itsm_source`

A user-managed list of sources (e.g., “ServiceNow – Client A”).

- `name` is the slug stored in `raw_snapshot.itsm_source`
- `display_label` is what the UI shows

### `itsm_field_mapping`

Maps a source field name (CSV column header) to a canonical field name.

During ingest:

- If a source column is mapped, it is stored under the canonical name.
- If it’s not mapped, UPMS stores it under the raw column name (fallback).
- If a mapping is marked `is_required` and missing from the CSV header, ingest fails validation.

---

## Ingest Pipeline

Ingest is orchestrated by `ISnapshotIngestService` (`UPMS.Web/Services`).

High-level flow:

1. User uploads a CSV and selects:
   - ITSM source
   - snapshot date
2. Validate required columns (via `IItsmFieldMappingService`)
3. Create `raw_snapshot`
4. For each CSV row:
   - determine `company_name` (from the canonical company field)
   - compute/receive `ticket_key`
   - upsert roster entry (`snapshot_ticket`)
   - write one `field_change` per field value

---

## Reporting Plugins

The report store (`/reports`) is driven by `IReportPlugin` implementations registered via DI.

- Plugins declare their own parameters (schema)
- Plugins return either:
  - an HTML preview, or
  - a downloadable file

For full details, see [`Docs/Reporting Plugins.md`](Reporting%20Plugins.md).

---

## Database Migrations

- Schema is managed through EF Core migrations (`src/UPMS.Data/Migrations`).
- `UPMS.Web` applies migrations automatically at startup.

---

## Notes / Known Constraints

- Ticket field names can vary by ITSM source. Reports and UI components should use canonical names where possible and perform case-insensitive lookup as a fallback.
- Very large snapshots may require batching/optimisation (current ingest approach is intentionally simple for MVP).
