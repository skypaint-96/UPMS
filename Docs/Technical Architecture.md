# Technical Architecture

---

## Project Structure

UPMS is composed of four .NET projects:

| Project | Type | Purpose |
|---------|------|---------|
| [`src/UPMS.Data/`](../src/UPMS.Data/) | Class library (.NET 10) | Core data access logic — snapshots, field changes, point-in-time reconstruction |
| [`src/UPMS.Web/`](../src/UPMS.Web/) | Blazor Server app (.NET 10) | Web portal — UI, upload handling, report store, reporting plugin host |
| [`tests/UPMS.Data.Tests/`](../tests/UPMS.Data.Tests/) | NUnit test project | Integration tests for the data access library (uses SQLite in-process) |
| [`tests/UPMS.Web.Tests/`](../tests/UPMS.Web.Tests/) | NUnit + Playwright test project | End-to-end UI tests for the Blazor web application |

There is no separate REST API backend, no React/TypeScript frontend, no worker service, and no object storage service. The Blazor Server application (`UPMS.Web`) is the single web-facing component.

---

## Application Architecture

### UPMS.Web — Blazor Server

`UPMS.Web` is a Blazor Server application using the **Interactive Server** rendering model. The browser connects to the server via a persistent SignalR connection. All UI logic executes server-side; the browser renders the resulting HTML and forwards events.

Current pages:

| Page | Route | Purpose |
|------|-------|---------|
| [`Home.razor`](../src/UPMS.Web/Components/Pages/Home.razor) | `/` | Dashboard / landing |
| [`Snapshots.razor`](../src/UPMS.Web/Components/Pages/Snapshots.razor) | `/snapshots` | List all snapshots |
| [`SnapshotDetail.razor`](../src/UPMS.Web/Components/Pages/SnapshotDetail.razor) | `/snapshots/{id}` | View tickets within a snapshot |
| [`Tickets.razor`](../src/UPMS.Web/Components/Pages/Tickets.razor) | `/tickets` | Browse tickets |
| [`TicketDetail.razor`](../src/UPMS.Web/Components/Pages/TicketDetail.razor) | `/tickets/{key}` | View a ticket's current state and field history |
| [`Upload.razor`](../src/UPMS.Web/Components/Pages/Upload.razor) | `/upload` | Upload a new snapshot file |

`UPMS.Web` references `UPMS.Data` directly as a project dependency and consumes [`TicketDataServiceInstance`](../src/UPMS.Data/TicketDataServiceInstance.cs) via dependency injection.

### UPMS.Data — Data Access Library

`UPMS.Data` is a pure data access library. It has no knowledge of Blazor, HTTP, or reporting. It exposes:

- [`TicketDataService`](../src/UPMS.Data/TicketDataService.cs) — static service class providing all data I/O operations
- [`TicketDataServiceInstance`](../src/UPMS.Data/TicketDataServiceInstance.cs) — DI wrapper around the static service, registered in `UPMS.Web`'s DI container
- Data types: [`Snapshot`](../src/UPMS.Data/Snapshot.cs), [`Ticket`](../src/UPMS.Data/Ticket.cs), [`FieldChange`](../src/UPMS.Data/FieldChange.cs), [`DatabaseOptions`](../src/UPMS.Data/DatabaseOptions.cs)

Database access uses **Dapper** with **Npgsql** for PostgreSQL.

---

## Database Schema

UPMS uses **PostgreSQL 15+**. The schema is defined in [`sql/migrations/`](../sql/migrations/).

### Table: `raw_snapshot`

One row per uploaded snapshot. Represents a batch of ticket data from one ITSM source at one point in time.

| Column | Type | Description |
|--------|------|-------------|
| `id` | `UUID` PK | Unique snapshot identifier |
| `company_id` | `UUID` | Tenant/company identifier |
| `itsm_source` | `VARCHAR` | Name of the source ITSM system (e.g. `"ServiceNow"`) |
| `snapshot_date` | `TIMESTAMPTZ` | The date/time the snapshot represents |
| `uploaded_by` | `VARCHAR` | User or system that uploaded the snapshot |
| `upload_metadata` | `JSONB` | Optional metadata about the upload |

### Table: `snapshot_ticket`

One row per ticket per snapshot. Acts as a roster — a fast lookup of which ticket keys appeared in a given snapshot.

| Column | Type | Description |
|--------|------|-------------|
| `id` | `UUID` PK | Row identifier |
| `snapshot_id` | `UUID` FK → `raw_snapshot.id` | The snapshot this ticket belongs to |
| `ticket_key` | `VARCHAR` | The ticket's unique identifier (e.g. `"INC0001234"`) |
| `company_id` | `UUID` | Tenant/company identifier |

**Unique constraint**: `(snapshot_id, ticket_key)` — a ticket key appears at most once per snapshot.

### Table: `field_change`

The core append-only history table. One row per field value observed per ticket per snapshot.

| Column | Type | Description |
|--------|------|-------------|
| `id` | `BIGINT` PK | Auto-incrementing row identifier |
| `ticket_key` | `VARCHAR` | The ticket this field belongs to |
| `field_name` | `VARCHAR` | Name of the field (e.g. `"Status"`) |
| `field_value` | `TEXT` nullable | The field's value at this observation |
| `observed_at` | `TIMESTAMPTZ` | When this value was observed |
| `snapshot_id` | `UUID` FK → `raw_snapshot.id` | The snapshot this observation came from |
| `company_id` | `UUID` | Tenant/company identifier |

**Point-in-time reconstruction** is performed by querying `DISTINCT ON (field_name) ORDER BY observed_at DESC WHERE observed_at <= :asOfTime`. This returns the last observed value for each field up to the requested timestamp, reconstructing the ticket's full state at that point.

All tenant isolation is by `company_id`.

---

## Canonical Field Mapping

> **Planned — not yet implemented.**

Different ITSM sources name semantically identical fields differently. To normalise data across sources, UPMS will maintain an `itsm_field_mapping` reference table:

| Column | Type | Description |
|--------|------|-------------|
| `itsm_source` | `VARCHAR` | The ITSM source (e.g. `"ServiceNow"`, `"Jira"`) |
| `source_field_name` | `VARCHAR` | The field name as it appears in the source system |
| `canonical_field_name` | `VARCHAR` | The normalised field name used within UPMS |

**Example mappings:**

| ITSM Source | Source Field Name | Canonical Field Name |
|-------------|-------------------|----------------------|
| ServiceNow  | `incident_state`  | `Status`             |
| Jira        | `status`          | `Status`             |
| ServiceNow  | `assigned_to`     | `Assignee`           |
| Jira        | `assignee`        | `Assignee`           |

A **mapping service** in `UPMS.Data` will look up source field names in this table during ingest and translate them to canonical names before writing `field_change` records. Once implemented, all data in `field_change` will use canonical field names, regardless of which ITSM source it originated from.

---

## Data Flow

```
File Upload (UPMS.Web — Upload.razor)
    │
    ▼
Create raw_snapshot record
    │
    ▼
Parse uploaded file (CSV / JSON)          ← Not yet implemented
    │
    ▼
For each ticket in file:
  └─ Insert snapshot_ticket record
  └─ For each field in ticket:
       └─ Look up canonical field name in itsm_field_mapping   ← Not yet implemented
       └─ Insert field_change record (using canonical field name)
    │
    ▼
Point-in-time query (UPMS.Data — TicketDataService)
  └─ SELECT DISTINCT ON (field_name) ... WHERE observed_at <= :asOf
  └─ Returns reconstructed Ticket with Fields dictionary
    │
    ▼
Display in UPMS.Web (Tickets / TicketDetail pages)
```

---

## Stored Procedures

Key stored procedures are defined in [`sql/stored-procedures/`](../sql/stored-procedures/):

| Procedure | Purpose |
|-----------|---------|
| [`get_ticket_at_time.sql`](../sql/stored-procedures/get_ticket_at_time.sql) | Reconstruct a single ticket's state at a given timestamp |
| [`get_tickets_for_snapshot.sql`](../sql/stored-procedures/get_tickets_for_snapshot.sql) | Retrieve all tickets for a snapshot at the snapshot date |
| [`batch_reconstruct_tickets.sql`](../sql/stored-procedures/batch_reconstruct_tickets.sql) | Reconstruct multiple tickets in a single set-based operation |

The database performs set-based windowing and grouping operations. Business logic remains in the application layer.

---

## Reporting Plugin System

> **Planned — not yet implemented.**

The reporting plugin system lives entirely within `UPMS.Web`. `UPMS.Data` has no knowledge of reporting.

### Design

Each reporting plugin implements an `IReportPlugin` interface (to be defined in `UPMS.Web`):

```csharp
public interface IReportPlugin
{
    string PluginId { get; }
    string DisplayName { get; }
    string Description { get; }
    IReadOnlyList<ReportParameterDefinition> Parameters { get; }
    Task<ReportResult> GenerateAsync(ReportRequest request, CancellationToken ct);
}
```

Plugins are discovered at application startup via .NET dependency injection. Plugin assemblies are scanned and all types implementing `IReportPlugin` are registered automatically.

### Report Store UI

The report store is a page in `UPMS.Web` that:

1. Lists all registered plugins and their available report options.
2. Dynamically renders a parameter form based on each plugin's `Parameters` schema.
3. Invokes the plugin's `GenerateAsync` method on submission.
4. Delivers the generated output (file download, HTML preview, etc.) to the user.

### Planned Plugin Types

| Plugin | Output Format | Description |
|--------|---------------|-------------|
| PowerPoint reporting pack | `.pptx` | Templated slide decks; user selects styling parameters and a date range |
| Email notification | `HTML` | Pre-defined email templates; generates ready-to-send HTML content |
| PDF export | `.pdf` | *(Potential future plugin)* |

Plugins provide their own template assets (PPTX templates, HTML templates). Templates are treated as data, not code, and are bundled with or uploaded alongside their plugin.

No plugin accesses the database directly. All data access goes through `UPMS.Data`.

---

## Architectural Principles

- **Append-only history** — field change records are never modified or deleted after ingest.
- **Deterministic reconstruction** — given the same data and the same `asOf` timestamp, reconstruction always produces the same result.
- **Single deployable unit** — one Blazor Server application and one PostgreSQL database. No microservices, no message queues, no object storage.
- **Plugin extensibility** — new report types are added as plugins without modifying the core application.
- **Data access isolation** — all database queries go through `UPMS.Data`; `UPMS.Web` never issues raw SQL.
- **Separation of concerns** — data access (`UPMS.Data`), UI and reporting host (`UPMS.Web`), and database logic (stored procedures) each have a single responsibility.
