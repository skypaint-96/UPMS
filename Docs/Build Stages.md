# Build Stages

This document tracks the “MVP ladder” for UPMS.

Status legend:

- ✅ **Complete**
- 🟡 **In progress**
- ⬜ **Not started**

---

## Stage 1 — Data model + schema

✅ Complete

Deliverables:

- EF Core model (`UpmsDbContext`) for:
  - `raw_snapshot`
  - `snapshot_ticket`
  - `field_change`
  - `itsm_source`
  - `itsm_field_mapping`
- EF Core migrations under `src/UPMS.Data/Migrations`
- Startup migration runner (web app runs migrations at boot)

---

## Stage 2 — Point-in-time ticket reconstruction

✅ Complete

Deliverables:

- `TicketDataService`:
  - create snapshots
  - store roster (`snapshot_ticket`)
  - store field history (`field_change`)
  - reconstruct a ticket “as-of” time T
  - list tickets “as-of” time T

---

## Stage 3 — ITSM source management

✅ Complete

Deliverables:

- `IItsmSourceService` + `ItsmSourceService`
- `IItsmFieldMappingService` + `ItsmFieldMappingService`
- UI pages:
  - `/itsm-sources`
  - `/itsm-sources/{name}`

---

## Stage 4 — Snapshot ingest (CSV upload)

✅ Complete

Deliverables:

- `ISnapshotIngestService` that:
  - validates required columns for the chosen ITSM source
  - creates a snapshot
  - writes tickets + field changes
- Upload UI (`/upload`)

---

## Stage 5 — Data browsing UI

✅ Complete

Deliverables:

- Snapshot browsing (`/snapshots`, `/snapshots/{id}`)
- Ticket searching (`/tickets`) with an **As Of** time input
- Ticket detail view with point-in-time reconstruction:
  - accepts `?asOf=yyyy-MM-ddTHH:mm`
  - supports linking from snapshots and ticket search

---

## Stage 6 — Reporting plugin system

✅ Complete

Deliverables:

- `IReportPlugin` contract
- `PluginRegistry`
- Report store UI (`/reports`)
- Download endpoint (`/reports/download/{token}`)

---

## Stage 7 — Example reports

✅ Complete

Deliverables:

- PowerPoint report plugin (PPTX download)
- Email notification plugin (HTML preview)
- Example plugins:
  - Status breakdown (HTML)
  - Ticket CSV export (CSV download)
  - Field delta between dates (HTML)

Documentation:

- [`Docs/Reporting Plugins.md`](Reporting%20Plugins.md)

---

## Stage 8 — Next steps (post-MVP)

⬜ Not started

Ideas:

- Report scheduling / automation
- Saved report configurations
- Permissions / multi-tenant access control
- Performance optimisation for very large snapshots
