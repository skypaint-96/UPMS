# Technical Architecture

---

## Project Structure

UPMS is composed of four .NET projects:

| Project | Type | Purpose |
|---------|------|---------|
| [`src/UPMS.Data/`](../src/UPMS.Data/) | Class library (.NET 10) | Core data access logic — snapshots, field changes, point-in-time reconstruction, ITSM source field mapping |
| [`src/UPMS.Web/`](../src/UPMS.Web/) | Blazor Server app (.NET 10) | Web portal — UI, upload handling, ITSM source management, report store, reporting plugin host |
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
| [`Upload.razor`](../src/UPMS.Web/Components/Pages/Upload.razor) | `/upload` | Upload a new CSV snapshot file |
| *(planned)* `ItsmSources.razor` | `/itsm-sources` | ITSM Source Management — add/edit/delete sources and their field mappings |

`UPMS.Web` references `UPMS.Data` directly as a project dependency and consumes [`TicketDataServiceInstance`](../src/UPMS.Data/TicketDataServiceInstance.cs) and [`IItsmFieldMappingService`](../src/UPMS.Data/IItsmFieldMappingService.cs) via dependency injection.

### UPMS.Data — Data Access Library

`UPMS.Data` is a pure data access library. It has no knowledge of Blazor, HTTP, or reporting. It exposes:

- [`TicketDataService`](../src/UPMS.Data/TicketDataService.cs) — static service class providing all snapshot, ticket, and field change I/O operations
- [`TicketDataServiceInstance`](../src/UPMS.Data/TicketDataServiceInstance.cs) — DI wrapper around the static service, registered in `UPMS.Web`'s DI container
- [`IItsmFieldMappingService`](../src/UPMS.Data/IItsmFieldMappingService.cs) — interface for canonical field name lookups and mapping management
- [`ItsmFieldMappingService`](../src/UPMS.Data/ItsmFieldMappingService.cs) — database-backed implementation of `IItsmFieldMappingService`
- Data types: [`Snapshot`](../src/UPMS.Data/Snapshot.cs), [`Ticket`](../src/UPMS.Data/Ticket.cs), [`FieldChange`](../src/UPMS.Data/FieldChange.cs), [`ItsmFieldMapping`](../src/UPMS.Data/ItsmFieldMapping.cs), [`DatabaseOptions`](../src/UPMS.Data/DatabaseOptions.cs)

Database access uses **Dapper** with **Npgsql** for PostgreSQL.

---

## Database Schema

UPMS uses **PostgreSQL 15+**. The schema is defined in [`sql/migrations/`](../sql/migrations/).

### Table: `itsm_source`

*(Planned — to be introduced in the Stage 3 migration.)*

One row per defined ITSM source instance. This is the parent table for field mappings.

| Column | Type | Description |
|--------|------|-------------|
| `id` | `UUID` PK | Unique source identifier |
| `name` | `VARCHAR` UNIQUE NOT NULL | Slug identifier used in snapshot records (e.g. `servicenow-client-a`) |
| `display_label` | `VARCHAR` NOT NULL | Human-readable name shown in the UI (e.g. `"ServiceNow — Client A"`) |
| `created_at` | `TIMESTAMPTZ` | Row creation timestamp |

The `name` column is the value stored in `raw_snapshot.itsm_source` and referenced by the field mapping table.

### Table: `itsm_field_mapping`

*(Introduced in [`sql/migrations/003_itsm_field_mapping.sql`](../sql/migrations/003_itsm_field_mapping.sql) — to be revised to add `is_required` and FK to `itsm_source`.)*

One row per field mapping per ITSM source instance.

| Column | Type | Description |
|--------|------|-------------|
| `itsm_source` | `VARCHAR` FK → `itsm_source.name` | The ITSM source instance this mapping belongs to |
| `source_field_name` | `VARCHAR` NOT NULL | The column name as it appears in the CSV export from this source (e.g. `short_description`) |
| `canonical_field_name` | `VARCHAR` NOT NULL | The normalised canonical name used throughout UPMS (e.g. `title`) |
| `is_required` | `BOOLEAN` NOT NULL DEFAULT false | Whether this column must be present in every uploaded CSV for this source |
| `created_at` | `TIMESTAMPTZ` | Row creation timestamp |
| `updated_at` | `TIMESTAMPTZ` | Last update timestamp |

**Primary key**: `(itsm_source, source_field_name)` — a source column name is mapped at most once per ITSM source.

**Index**: `idx_itsm_field_mapping_source` on `(itsm_source)` for bulk lookup by source.

**Behaviour during ingest:**
- Any CSV column that has a row in this table for the selected ITSM source is stored under the `canonical_field_name`.
- Any CSV column with no row in this table is stored under the raw source column name (graceful fallback).
- Any `source_field_name` with `is_required = true` that is absent from the CSV header row causes a validation error; the upload is rejected.

### Table: `raw_snapshot`

One row per uploaded snapshot. Represents a batch of ticket data from one ITSM source at one point in time.

| Column | Type | Description |
|--------|------|-------------|
| `id` | `UUID` PK | Unique snapshot identifier |
| `company_id` | `UUID` | Tenant/company identifier (legacy — company is now read from ticket data) |
| `itsm_source` | `VARCHAR` | Name of the ITSM source instance (references `itsm_source.name`) |
| `snapshot_date` | `TIMESTAMPTZ` | The date/time the snapshot represents |
| `uploaded_by` | `VARCHAR` | User or system that uploaded the snapshot |
| `uploaded_at` | `TIMESTAMPTZ` | When the upload occurred |
| `upload_metadata` | `JSONB` | Optional metadata about the upload |

### Table: `snapshot_ticket`

One row per ticket per snapshot. Acts as a roster — a fast lookup of which ticket keys appeared in a given snapshot.

| Column | Type | Description |
|--------|------|-------------|
| `id` | `UUID` PK | Row identifier |
| `snapshot_id` | `UUID` FK → `raw_snapshot.id` | The snapshot this ticket belongs to |
| `ticket_key` | `VARCHAR` | The ticket's unique identifier (e.g. `INC0001234`) |
| `company_id` | `UUID` | Tenant/company identifier |

**Unique constraint**: `(snapshot_id, ticket_key)` — a ticket key appears at most once per snapshot.

### Table: `field_change`

The core append-only history table. One row per field value observed per ticket per snapshot.

| Column | Type | Description |
|--------|------|-------------|
| `id` | `BIGINT` PK | Auto-incrementing row identifier |
| `ticket_key` | `VARCHAR` | The ticket this field belongs to |
| `field_name` | `VARCHAR` | Name of the field — either a canonical name (if mapped) or the raw source column name |
| `field_value` | `TEXT` nullable | The field's value at this observation |
| `observed_at` | `TIMESTAMPTZ` | When this value was observed (set to the snapshot date) |
| `snapshot_id` | `UUID` FK → `raw_snapshot.id` | The snapshot this observation came from |
| `company_id` | `UUID` | Tenant/company identifier |

**Point-in-time reconstruction** is performed by querying `DISTINCT ON (field_name) ORDER BY observed_at DESC WHERE observed_at <= :asOfTime`. This returns the last observed value for each field up to the requested timestamp, reconstructing the ticket's full state at that point.

All tenant isolation is by `company_id`.

---

## ITSM Source Definition Service

The [`IItsmFieldMappingService`](../src/UPMS.Data/IItsmFieldMappingService.cs) interface in `UPMS.Data` provides the canonical field mapping operations used during ingest and by the ITSM Source Management UI:

```csharp
public interface IItsmFieldMappingService
{
    // Returns the canonical name for a given source field name.
    // Falls back to sourceFieldName if no mapping is found.
    string GetCanonicalName(string itsmSource, string sourceFieldName);

    // Returns all mappings for a given ITSM source.
    IEnumerable<ItsmFieldMapping> GetMappingsForSource(string itsmSource);

    // Adds or updates a single field mapping.
    Task UpsertMappingAsync(string itsmSource, string sourceFieldName, string canonicalFieldName);
}
```

The concrete implementation [`ItsmFieldMappingService`](../src/UPMS.Data/ItsmFieldMappingService.cs) is database-backed via Dapper and is created via:

```csharp
ItsmFieldMappingService.CreateFromOptions(IOptions<DatabaseOptions> options)
```

The [`ItsmFieldMapping`](../src/UPMS.Data/ItsmFieldMapping.cs) data type represents a single mapping row:

```csharp
public class ItsmFieldMapping
{
    public required string ItsmSource { get; init; }
    public required string SourceFieldName { get; init; }
    public required string CanonicalFieldName { get; init; }
}
```

> **Note:** The `is_required` flag and the parent `itsm_source` table are planned database additions. The service interface will be extended to expose required-field management once that migration is applied.

---

## Ingest Pipeline

The ingest pipeline is implemented in [`ISnapshotIngestService`](../src/UPMS.Web/Services/ISnapshotIngestService.cs) within `UPMS.Web`.

### CSV Ingest Flow

```
File Upload (Upload.razor)
    │  User selects: ITSM source name, snapshot date, CSV file
    │  (company name is NOT collected — it comes from the CSV data)
    ▼
ISnapshotIngestService.IngestCsvAsync(fileStream, itsmSource, snapshotDate, uploadedBy, ...)
    │
    ▼
Validate CSV header row against required fields for this ITSM source
    │  → If any required source_field_name is missing from the header: return failure IngestResult
    │
    ▼
Create raw_snapshot record  (TicketDataService.CreateSnapshotAsync)
    │
    ▼
For each data row in the CSV:
    │
    ├─ Read the company value from the column mapped to canonical name "company"
    │
    ├─ Insert snapshot_ticket record  (TicketDataService.AddTicketsToSnapshotAsync)
    │
    └─ For each column in the row:
         ├─ Look up canonical name: IItsmFieldMappingService.GetCanonicalName(itsmSource, columnName)
         │    → Returns canonical name if mapped; falls back to raw column name if not
         └─ Insert field_change record  (TicketDataService.RecordFieldChangeAsync)
              field_name = canonical name (or raw column name if unmapped)
    │
    ▼
Return IngestResult
    │  Success = true
    │  TicketsIngested = number of rows processed
    │  FieldChangesRecorded = total field_change records inserted
    │  Warnings = any non-fatal issues (e.g. rows skipped due to missing ticket key)
```

### IngestResult

[`IngestResult`](../src/UPMS.Web/Services/IngestResult.cs) is the return type from both `IngestCsvAsync` and `IngestJsonAsync`:

```csharp
public class IngestResult
{
    public required bool Success { get; init; }
    public required Guid SnapshotId { get; init; }
    public required int TicketsIngested { get; init; }
    public required int FieldChangesRecorded { get; init; }
    public string? ErrorMessage { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
```

---

## Data Flow

```
File Upload (UPMS.Web — Upload.razor)
    │  Inputs: ITSM source (selected from defined sources), snapshot date, CSV file
    │
    ▼
ISnapshotIngestService.IngestCsvAsync
    │
    ├─ Validate required fields against CSV header
    │
    ├─ Create raw_snapshot record
    │
    └─ For each CSV row (one ticket):
         ├─ Read company from mapped "company" column
         ├─ Insert snapshot_ticket record
         └─ For each column:
              IItsmFieldMappingService.GetCanonicalName → Insert field_change record
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
- **Source-instance mapping** — field mappings are scoped to a specific named ITSM source instance, not to a tool type. The `itsm_source` value in all tables is the ITSM source `name` (e.g. `servicenow-client-a`), not a generic tool label.
- **Company from data, not metadata** — the company for each ticket is read from the CSV row via the field mapping for canonical name `company`; it is not a separate upload-time input.
- **Single deployable unit** — one Blazor Server application and one PostgreSQL database. No microservices, no message queues, no object storage.
- **Plugin extensibility** — new report types are added as plugins without modifying the core application.
- **Data access isolation** — all database queries go through `UPMS.Data`; `UPMS.Web` never issues raw SQL.
- **Separation of concerns** — data access (`UPMS.Data`), UI and reporting host (`UPMS.Web`), and database logic (stored procedures) each have a single responsibility.
