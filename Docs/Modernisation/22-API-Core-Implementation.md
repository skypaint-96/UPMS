# API Core Implementation

## Implemented projects and files

### New runtime project
- `src/UPMS.Api/`

### New extracted libraries used by the API
- `src/UPMS.Ingestion/`
- `src/UPMS.Reporting/`

## What was implemented

### Routing surface
The API now exposes versioned minimal API endpoints under `/api/v1` for:

- health
- canonical fields
- ITSM sources and mapping maintenance
- snapshots and snapshot tickets
- tickets and ticket history
- synchronous snapshot ingest
- report plugin discovery
- synchronous report execution
- asynchronous job submission and job status/download

### Authentication mode
The API supports a simple configurable API-key mode using the `X-UPMS-API-Key` header.

Configuration path:

- `Auth:Mode`
- `Auth:ApiKey:HeaderName`
- `Auth:ApiKey:Keys`

### Shared-library extraction
Existing monolithic logic was extracted so API and worker can share the same implementation:

- CSV/JSON ingest moved into `UPMS.Ingestion`
- report plugins/templates moved into `UPMS.Reporting`

### Shared services added to `UPMS.Data`
The data layer now also contains:

- artifact storage abstraction
- file-system artifact storage implementation
- background job entity/service

### Tests added
A new API test project was added:

- `tests/UPMS.Api.Tests/`

The test project seeds an in-memory `UpmsDbContext` and verifies representative API routes and job submission behaviour.

## Design notes

### Why synchronous and asynchronous endpoints both exist
Direct synchronous endpoints are useful for:

- local development
- admin workflows
- debugging
- small exports
- lightweight integration tests

Async worker-backed endpoints are better for:

- large snapshot files
- slow report generation
- file-producing jobs
- resilient containerised workloads
