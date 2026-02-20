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

## Stage 3 — Canonical Field Mapping

**Status: 🔲 NOT STARTED**

**Goal:** Implement the ITSM source field name normalisation layer. Different ITSM sources use different field names for semantically identical fields. This stage introduces a reference mapping table and a mapping service so that all field data is stored using consistent canonical field names regardless of source.

### Acceptance Criteria
- A new `itsm_field_mapping` table exists with columns `itsm_source`, `source_field_name`, `canonical_field_name`.
- A migration script creates and seeds the table with known mappings for supported ITSM sources (e.g. ServiceNow, Jira).
- A mapping service in `UPMS.Data` can look up the canonical name for a given source and source field name.
- If no mapping exists, the source field name is used as-is (graceful fallback).
- The mapping service has unit tests covering lookup, fallback, and multi-source scenarios.

### Key Files / Components
| File | Description |
|------|-------------|
| `sql/migrations/003_itsm_field_mapping.sql` | Creates and seeds the `itsm_field_mapping` table |
| `src/UPMS.Data/ItsmFieldMappingService.cs` | Service for looking up canonical field names |
| `src/UPMS.Data/ItsmFieldMapping.cs` | Data type representing a single mapping row |
| `tests/UPMS.Data.Tests/ItsmFieldMappingServiceTests.cs` | Unit/integration tests for the mapping service |

---

## Stage 4 — File Ingest / Upload Parsing

**Status: 🔲 NOT STARTED** *(Partial: snapshot record creation exists)*

**Goal:** Complete the upload pipeline so that uploading a file actually parses its contents and populates `snapshot_ticket` and `field_change` records. Currently the Upload page creates a `raw_snapshot` record but does not read or parse the uploaded file.

### Acceptance Criteria
- CSV and/or JSON snapshot files can be uploaded via the Upload page.
- The parser reads each row/record and extracts ticket keys and field values.
- Source field names are looked up in `itsm_field_mapping` and translated to canonical names before being stored (depends on Stage 3).
- A `snapshot_ticket` record is inserted for each ticket found.
- A `field_change` record is inserted for each field value in each ticket.
- Re-uploading the same snapshot does not corrupt existing data (idempotent or guarded).
- The Upload page displays a summary of what was ingested (tickets found, field changes recorded).

### Key Files / Components
| File | Description |
|------|-------------|
| [`src/UPMS.Web/Components/Pages/Upload.razor`](../src/UPMS.Web/Components/Pages/Upload.razor) | Upload page — needs file parsing wired in |
| `src/UPMS.Web/Services/SnapshotIngestService.cs` | New: orchestrates parsing and DB writes |
| `src/UPMS.Web/Parsers/CsvSnapshotParser.cs` | New: CSV file parser |
| `src/UPMS.Web/Parsers/JsonSnapshotParser.cs` | New: JSON file parser (if supported) |
| [`tests/UPMS.Web.Tests/UploadPageTests.cs`](../tests/UPMS.Web.Tests/UploadPageTests.cs) | E2E tests for upload flow |

---

## Stage 5 — Reporting Plugin System Foundation

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

## Stage 6 — First Reporting Plugin: PowerPoint Pack

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

## Stage 7 — Second Reporting Plugin: Email Notification

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

## Stage 8 — Validation, Polish, and Performance

**Status: 🔲 NOT STARTED**

**Goal:** Address known gaps, hardcoded values, and quality issues across the application to bring it to a production-ready state.

### Acceptance Criteria
- The `itsmSources` dropdown on the Snapshots page is populated from the database, not hardcoded.
- Company names / identifiers are not hardcoded in any page.
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
