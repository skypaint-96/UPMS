# Current Solution Review

## What the existing UPMS does well

The current UPMS repository is already a solid domain-first system for historical problem-management data. The existing application is a **single deployable Blazor Server web app** backed by PostgreSQL and a shared `.NET 10` data library.

The current platform purpose is clear:

- ingest point-in-time exports from multiple ITSM tools or tool instances
- normalize source-specific field names into a canonical model
- store ticket history as an append-only field change log
- reconstruct tickets exactly as they looked at a chosen time
- provide reporting and document-generation plugins for operational outputs

## Existing capabilities discovered in the repository

### Data platform

The current `UPMS.Data` library already supports:

- snapshot creation and retrieval
- snapshot roster storage (`snapshot_ticket`)
- append-only field history (`field_change`)
- point-in-time ticket reconstruction
- ticket search by source/company/as-of time
- ticket history retrieval
- canonical field definition management
- ITSM source and field-mapping management
- EF Core migrations and PostgreSQL schema bootstrapping

### User-facing web experience

The existing `UPMS.Web` application already provides:

- dashboard/home page
- snapshot browsing and detail pages
- ticket search and ticket detail pages
- CSV/JSON upload workflow
- ITSM source management UI
- report store UI
- report template storage UI
- document export and download endpoints

### Reporting platform

The current reporting/plugin model already includes:

- plugin discovery and registration
- HTML preview style reports
- downloadable file reports
- PowerPoint generation
- email draft generation
- ticket document export
- tokenized template filling
- example CSV, field-delta, lifecycle, and status-breakdown reports

### Quality signals already present

The existing repo already contains meaningful automated tests for:

- data reconstruction logic
- point-in-time behaviour
- multi-tenant isolation across companies/sources
- ingest behaviour
- report/plugin registration
- UI routes and page behaviour in the legacy Blazor app

## Current architectural constraint

The main constraint is not the domain model; it is the deployment shape.

Today the application is still primarily a **monolithic web process**:

- no separate API boundary
- no separate React SPA
- no separate worker process for long-running jobs
- reporting and ingest logic are hosted directly inside the web app

That makes the current solution harder to:

- expose safely to external consumers such as Excel Power Query
- scale independently by workload type
- move large ingest/report operations off the request thread
- adopt a modern frontend independently of backend changes

## Modernisation approach used in this implementation

This implementation keeps the existing strengths and changes the delivery model around them.

The modernization therefore follows this rule:

> Preserve the proven historical-ticket domain model and reporting behaviour, then split runtime responsibilities into dedicated components.

The resulting target shape is:

- `UPMS.Data` as the shared core data/platform library
- `UPMS.Ingestion` as reusable ingest orchestration
- `UPMS.Reporting` as reusable report orchestration and plugins
- `UPMS.Api` as the primary application boundary
- `UPMS.Worker` for background ingest/report jobs
- `UPMS.Frontend` as the primary CGI EDS React user interface
- `UPMS.Web` retained as a legacy reference/transition component
