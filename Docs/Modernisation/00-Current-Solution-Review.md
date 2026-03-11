# Current Solution Review

## What the existing UPMS does well

The current UPMS repository is a domain-first system for historical problem-management data built around a split API, worker, frontend, and shared `.NET 10` libraries.

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

### User-facing application surface

The active API, worker, and React frontend stack provides:

- dashboard and ticket browsing pages
- snapshot browsing and upload workflows
- ITSM source management UI
- report generation and template-library workflows
- background job execution and artifact download endpoints

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
- API routes, data workflows, and the active application surface

## Current architectural posture

The main strength of the current repository is that the runtime shape now matches the target delivery model:

- API boundary for frontend and external consumers
- separate React SPA
- separate worker process for long-running jobs
- shared ingestion and reporting libraries reused across runtimes

That keeps the domain model reusable while allowing frontend, API, and job execution concerns to evolve independently.

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
