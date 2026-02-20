# Build Stages

This document defines the implementation stages for UPMS. Each stage has a clear goal, acceptance criteria, and a list of the key files and components affected.

**Status key:**
- ✅ **COMPLETE** — implemented and tested
- 🔲 **NOT STARTED** — not yet begun

---

## Stage 1 — Database Foundation

**Status: ✅ COMPLETE**

**Goal:** Establish the append-only historical data model in PostgreSQL, including all tables, indexes, and stored procedures needed for point-in-time reconstruction.

### Acceptance Criteria
- `raw_snapshot`, `snapshot_ticket`, and `field_change` tables exist with correct columns and constraints.
- Indexes are in place for efficient ticket reconstruction, snapshot lookup, and company-scoped queries.
- Stored procedures return correct results for point-in-time reconstruction.
- Schema is reproducible from the migration scripts alone.

### Key Files / Components
| File | Description |
|------|-------------|
| [`sql/migrations/001_initial_schema.sql`](../sql/migrations/001_initial_schema.sql) | Creates the three core tables |
| [`sql/migrations/002_indexes.sql`](../sql/migrations/002_indexes.sql) | Adds performance indexes |
| [`sql/stored-procedures/get_ticket_at_time.sql`](../sql/stored-procedures/get_ticket_at_time.sql) | Reconstructs a single ticket at a given timestamp |
| [`sql/stored-procedures/get_tickets_for_snapshot.sql`](../sql/stored-procedures/get_tickets_for_snapshot.sql) | Retrieves all tickets for a snapshot |
| [`sql/stored-procedures/batch_reconstruct_tickets.sql`](../sql/stored-procedures/batch_reconstruct_tickets.sql) | Batch reconstruction for reporting |

---

## Stage 2 — Core Data Access Library

**Status: ✅ COMPLETE**

**Goal:** Implement `UPMS.Data` — the data access library that wraps PostgreSQL operations, exposes snapshot and field change management, and supports point-in-time ticket reconstruction.

### Acceptance Criteria
- Snapshots can be created and retrieved.
- Tickets can be added to snapshots.
- Field changes can be recorded.
- Tickets can be reconstructed at any point in time via `GetTicketsAsync` and `GetTicketsBySnapshotAsync`.
- Field change history can be retrieved per ticket per field.
- Multi-tenant isolation by company is enforced.
- Integration tests pass against a real database.

### Key Files / Components
| File | Description |
|------|-------------|
| [`src/UPMS.Data/TicketDataService.cs`](../src/UPMS.Data/TicketDataService.cs) | Static service with all data I/O operations |
| [`src/UPMS.Data/TicketDataServiceInstance.cs`](../src/UPMS.Data/TicketDataServiceInstance.cs) | DI wrapper for `TicketDataService` |
| [`src/UPMS.Data/Snapshot.cs`](../src/UPMS.Data/Snapshot.cs) | Snapshot data type |
| [`src/UPMS.Data/Ticket.cs`](../src/UPMS.Data/Ticket.cs) | Ticket data type |
| [`src/UPMS.Data/FieldChange.cs`](../src/UPMS.Data/FieldChange.cs) | FieldChange data type |
| [`src/UPMS.Data/DatabaseOptions.cs`](../src/UPMS.Data/DatabaseOptions.cs) | Database configuration options |
| [`tests/UPMS.Data.Tests/`](../tests/UPMS.Data.Tests/) | Integration test suite |

---

## Stage 3 — ITSM Source Definition and Canonical Field Mapping

**Status: 🔲 NOT STARTED**

**Goal:** Introduce the two-table ITSM source definition model. An ITSM source is a **named instance** of a ticketing tool (not just a tool type). Each source has a set of field mappings (source column name → canonical name) and a set of required field names used for upload validation. This stage replaces the existing single-table `itsm_field_mapping` design with the correct two-table model.

### Design

The new schema consists of:

**`itsm_source` table** — one row per defined ITSM source instance:
- `id` (UUID PK)
- `name` (VARCHAR UNIQUE) — slug identifier used in snapshot records (e.g. `servicenow-client-a`)
- `display_label` (VARCHAR) — human-readable label shown in the UI

**`itsm_field_mapping` table** — revised to be a child of `itsm_source`:
- `itsm_source` (VARCHAR FK → `itsm_source.name`) — replaces the free-text ITSM source column
- `source_field_name` (VARCHAR) — the column name as it appears in the CSV export
- `canonical_field_name` (VARCHAR) — the normalised canonical name used throughout UPMS
- `is_required` (BOOLEAN DEFAULT false) — if true, this column must be present in every uploaded CSV for this source

The existing [`sql/migrations/003_itsm_field_mapping.sql`](../sql/migrations/003_itsm_field_mapping.sql) (which created a simpler version of `itsm_field_mapping` without `is_required` or the parent `itsm_source` table) must be superseded by a new migration that introduces the correct two-table design.

### Acceptance Criteria
- A new `itsm_source` table exists with `id`, `name`, and `display_label` columns.
- The `itsm_field_mapping` table has `itsm_source` (FK → `itsm_source.name`), `source_field_name`, `canonical_field_name`, and `is_required` columns.
- The primary key on `itsm_field_mapping` is `(itsm_source, source_field_name)`.
- `IItsmFieldMappingService.GetCanonicalName` returns the canonical name for a given source and source field name, falling back to the source field name if no mapping exists.
- `IItsmFieldMappingService.GetMappingsForSource` returns all mappings for a given ITSM source.
- `IItsmFieldMappingService.UpsertMappingAsync` adds or updates a mapping.
- A method exists to retrieve required field names for a given ITSM source (for upload validation).
- The mapping service has integration tests covering lookup, fallback, required-field retrieval, and multi-source scenarios.

### Key Files / Components
| File | Description |
|------|-------------|
| `sql/migrations/004_itsm_source_definition.sql` | New migration: creates `itsm_source` table and revises `itsm_field_mapping` with `is_required` and FK |
| [`src/UPMS.Data/IItsmFieldMappingService.cs`](../src/UPMS.Data/IItsmFieldMappingService.cs) | Interface — extend to expose required-field retrieval |
| [`src/UPMS.Data/ItsmFieldMappingService.cs`](../src/UPMS.Data/ItsmFieldMappingService.cs) | Implementation — update queries to use new schema |
| [`src/UPMS.Data/ItsmFieldMapping.cs`](../src/UPMS.Data/ItsmFieldMapping.cs) | Data type — add `IsRequired` property |
| `src/UPMS.Data/ItsmSource.cs` | New data type representing a row from the `itsm_source` table |
| [`tests/UPMS.Data.Tests/ItsmFieldMappingServiceTests.cs`](../tests/UPMS.Data.Tests/ItsmFieldMappingServiceTests.cs) | Update/extend tests for the new schema |

---

## Stage 4 — ITSM Source Management UI

**Status: 🔲 NOT STARTED**

**Goal:** Provide a web page in UPMS where users can view, add, edit, and delete ITSM source definitions and their field mappings. This is the administrative interface for the data introduced in Stage 3.

### Acceptance Criteria
- A new page at `/itsm-sources` lists all defined ITSM sources (name and display label).
- Users can add a new ITSM source by entering a name and display label.
- Users can edit the field mappings for an ITSM source: add, update, or remove mappings (source column name → canonical name, and whether the field is required).
- Users can delete an ITSM source (and its associated field mappings).
- The ITSM source name (slug) is validated to be unique and non-empty.
- The Upload page's ITSM source dropdown is populated from the `itsm_source` table, not hardcoded values.
- Navigation includes a link to the ITSM Source Management page.

### Key Files / Components
| File | Description |
|------|-------------|
| `src/UPMS.Web/Components/Pages/ItsmSources.razor` | New page: list and manage ITSM sources |
| [`src/UPMS.Web/Components/Pages/Upload.razor`](../src/UPMS.Web/Components/Pages/Upload.razor) | Update ITSM source dropdown to load from database |
| [`src/UPMS.Web/Components/Layout/MainLayout.razor`](../src/UPMS.Web/Components/Layout/MainLayout.razor) | Add navigation link to ITSM Source Management |
| [`src/UPMS.Data/IItsmFieldMappingService.cs`](../src/UPMS.Data/IItsmFieldMappingService.cs) | Consumed by the management page for all CRUD operations |

---

## Stage 5 — Flat-Table CSV Ingest

**Status: 🔲 NOT STARTED** *(Partial: snapshot record creation and service interface exist)*

**Goal:** Complete the upload pipeline to parse flat-table CSV snapshot files and populate `snapshot_ticket` and `field_change` records. The CSV format is: row 1 = header (source column names), each subsequent row = one ticket in its current state. Company comes from the ticket data, not from the upload form.

### CSV Format

```
number,company,short_description,priority,state
INC0001234,Acme Corp,Cannot login to VPN,High,In Progress
INC0001235,Globex Ltd,Email not syncing,Medium,New
```

Row 1 is the header. Each subsequent row is one ticket. The column that provides the company name is identified by the field mapping for canonical name `company` on the selected ITSM source.

### Acceptance Criteria
- CSV files can be uploaded via the Upload page.
- Row 1 is treated as the header row (source column names from the ITSM export).
- Required fields (as defined in `itsm_field_mapping.is_required` for the selected source) are validated against the header row before any data is written; missing required fields cause a validation error listing which fields are absent.
- The company value for each ticket is read from the column mapped to canonical name `company` for the selected ITSM source.
- Company is NOT collected as a separate input on the Upload page.
- For each data row, a `snapshot_ticket` record and one `field_change` record per column are inserted.
- Mapped columns are stored under their canonical name; unmapped columns are stored under their raw source column name.
- Re-uploading the same snapshot does not corrupt existing data (idempotent or guarded).
- The Upload page displays a summary: tickets ingested, field changes recorded, and any warnings.
- [`IngestResult`](../src/UPMS.Web/Services/IngestResult.cs) is returned from [`ISnapshotIngestService.IngestCsvAsync`](../src/UPMS.Web/Services/ISnapshotIngestService.cs) with `Success`, `TicketsIngested`, `FieldChangesRecorded`, and `Warnings` populated.

### Key Files / Components
| File | Description |
|------|-------------|
| [`src/UPMS.Web/Components/Pages/Upload.razor`](../src/UPMS.Web/Components/Pages/Upload.razor) | Remove company name input; add required-field validation feedback |
| [`src/UPMS.Web/Services/ISnapshotIngestService.cs`](../src/UPMS.Web/Services/ISnapshotIngestService.cs) | Interface — `IngestCsvAsync` signature already defined |
| `src/UPMS.Web/Services/SnapshotIngestService.cs` | Implement flat-table CSV parsing and DB writes |
| `src/UPMS.Web/Parsers/CsvSnapshotParser.cs` | New: flat-table CSV parser |
| [`src/UPMS.Web/Services/IngestResult.cs`](../src/UPMS.Web/Services/IngestResult.cs) | Return type — already defined |
| [`tests/UPMS.Web.Tests/IngestServiceTests.cs`](../tests/UPMS.Web.Tests/IngestServiceTests.cs) | Tests for ingest logic |
| [`tests/UPMS.Web.Tests/UploadPageTests.cs`](../tests/UPMS.Web.Tests/UploadPageTests.cs) | E2E tests for upload flow |

---

## Stage 6 — Reporting Plugin System Foundation

**Status: 🔲 NOT STARTED**

**Goal:** Build the plugin infrastructure in `UPMS.Web`: the `IReportPlugin` interface, plugin discovery via dependency injection, and the Report Store UI page where users can browse and launch available reports.

### Acceptance Criteria
- `IReportPlugin` interface is defined with `PluginId`, `DisplayName`, `Description`, `Parameters`, and `GenerateAsync`.
- `ReportParameterDefinition` and `ReportRequest` / `ReportResult` types are defined.
- Plugins are discovered at startup by scanning registered assemblies for `IReportPlugin` implementations.
- A Report Store page lists all registered plugins with their names and descriptions.
- Selecting a plugin renders a dynamic parameter form based on the plugin's `Parameters` schema.
- Submitting the form invokes `GenerateAsync` and delivers the result to the user.
- A no-op stub plugin exists for testing the infrastructure end-to-end.

### Key Files / Components
| File | Description |
|------|-------------|
| `src/UPMS.Web/Plugins/IReportPlugin.cs` | Plugin interface |
| `src/UPMS.Web/Plugins/ReportParameterDefinition.cs` | Parameter schema type |
| `src/UPMS.Web/Plugins/ReportRequest.cs` | Input model passed to plugins |
| `src/UPMS.Web/Plugins/ReportResult.cs` | Output model returned by plugins |
| `src/UPMS.Web/Plugins/PluginRegistry.cs` | DI-based plugin discovery and registry |
| `src/UPMS.Web/Components/Pages/ReportStore.razor` | Report Store UI page |
| `src/UPMS.Web/Components/Shared/ReportParameterForm.razor` | Dynamic parameter form component |

---

## Stage 7 — First Reporting Plugin: PowerPoint Pack

**Status: 🔲 NOT STARTED**

**Goal:** Implement the first concrete reporting plugin — a PowerPoint reporting pack that generates templated `.pptx` slide decks from ticket data. Users select a company, date range, and styling parameters, and receive a populated PPTX file as a download.

### Acceptance Criteria
- The plugin implements `IReportPlugin` and is discoverable by the plugin registry.
- The plugin declares parameters for: company, date range, and at minimum one styling option.
- PPTX generation uses a template file; placeholder tokens in the template are replaced with data values.
- The plugin retrieves ticket data via `UPMS.Data` (not by querying the database directly).
- The generated file is returned as a download via the Report Store UI.
- At least one slide template is included and populated with real data.

### Key Files / Components
| File | Description |
|------|-------------|
| `src/UPMS.Web/Plugins/PowerPoint/PowerPointReportPlugin.cs` | Plugin implementation |
| `src/UPMS.Web/Plugins/PowerPoint/PowerPointGenerator.cs` | PPTX generation logic |
| `src/UPMS.Web/Plugins/PowerPoint/Templates/` | PPTX template assets |

---

## Stage 8 — Second Reporting Plugin: Email Notification

**Status: 🔲 NOT STARTED**

**Goal:** Implement an email notification plugin that presents users with a choice of pre-defined HTML email templates, populates them with ticket data, and generates ready-to-send HTML output (with optional SMTP dispatch).

### Acceptance Criteria
- The plugin implements `IReportPlugin` and is discoverable by the plugin registry.
- The plugin exposes at least two distinct email template options as selectable parameters.
- HTML templates contain placeholders that are populated with ticket data at generation time.
- The plugin returns rendered HTML output via `ReportResult`.
- Optional: the plugin can send the email directly via SMTP if configured.
- The plugin retrieves ticket data via `UPMS.Data` only.

### Key Files / Components
| File | Description |
|------|-------------|
| `src/UPMS.Web/Plugins/Email/EmailNotificationPlugin.cs` | Plugin implementation |
| `src/UPMS.Web/Plugins/Email/EmailTemplateRenderer.cs` | HTML template rendering logic |
| `src/UPMS.Web/Plugins/Email/Templates/` | HTML email template assets |
| `src/UPMS.Web/Plugins/Email/SmtpDispatcher.cs` | Optional SMTP send logic |

---

## Stage 9 — Validation, Polish, and Performance

**Status: 🔲 NOT STARTED**

**Goal:** Address known gaps, hardcoded values, and quality issues across the application to bring it to a production-ready state.

### Acceptance Criteria
- The `itsmSources` dropdown on the Snapshots page is populated from the `itsm_source` table, not hardcoded.
- The Upload page company name input has been removed; company is read from CSV data.
- N+1 query patterns are identified and resolved (batch queries where applicable).
- Stored procedures are used for all bulk read operations where they exist.
- All `UPMS.Data.Tests` pass with full coverage of snapshot, field change, and point-in-time scenarios.
- All `UPMS.Web.Tests` E2E tests pass against a running application.
- Reconstructed ticket data is validated against known source exports for accuracy.
- `EXPLAIN ANALYZE` confirms index usage on key queries.

### Key Files / Components
| File | Description |
|------|-------------|
| [`src/UPMS.Web/Components/Pages/Snapshots.razor`](../src/UPMS.Web/Components/Pages/Snapshots.razor) | Fix hardcoded ITSM source dropdown |
| [`src/UPMS.Data/TicketDataService.cs`](../src/UPMS.Data/TicketDataService.cs) | Review for N+1 patterns |
| [`tests/UPMS.Data.Tests/`](../tests/UPMS.Data.Tests/) | Ensure full test coverage |
| [`tests/UPMS.Web.Tests/`](../tests/UPMS.Web.Tests/) | Ensure all E2E tests pass |

---

## Build Philosophy

> Build for **correctness and completeness** first.
> Optimise only when measurement proves it necessary.
>
> Each stage should leave the system in a fully working, testable state before the next stage begins.
