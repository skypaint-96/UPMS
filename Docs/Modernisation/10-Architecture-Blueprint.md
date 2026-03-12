# Target Architecture Blueprint

## Target runtime components

### 1. `UPMS.Api` (`.NET 10`)

Primary application boundary for:

- frontend reads/writes
- worker orchestration APIs
- external consumer access
- synchronous data access and lightweight report execution
- asynchronous job submission and job status retrieval

### 2. `UPMS.Frontend` (React 19 + CGI EDS)

Primary browser experience for:

- snapshot browsing
- ticket search and detail views
- ITSM source browsing
- upload/job submission
- report job submission and result collection

### 3. `UPMS.Worker` (`.NET 10`)

Dedicated background processor for:

- queued snapshot ingest
- queued report execution
- writing downloadable artifacts to shared storage

### 4. Shared libraries

- `UPMS.Data`: persistence, reconstruction, schema, job queue, artifact storage
- `UPMS.Ingestion`: CSV/JSON ingest orchestration
- `UPMS.Reporting`: report plugins, template store, report execution service

## Why this split is the right fit

This split preserves a single domain model while separating runtime concerns.

- API scales by request volume.
- Worker scales by ingest/report workload.
- Frontend deploys independently of backend implementation.
- External consumers can use stable JSON/file endpoints without coupling to Blazor.

## Primary request flows

### Frontend data flow

1. React frontend calls `UPMS.Api`.
2. API reads from `UPMS.Data` / PostgreSQL.
3. API returns JSON for UI rendering.

### Async ingest flow

1. Frontend uploads file to `UPMS.Api`.
2. API stores raw file in shared artifact storage.
3. API writes `background_job` row with job type `snapshot-ingest`.
4. Worker claims the job and executes `UPMS.Ingestion`.
5. Worker marks the job `succeeded` or `failed`.
6. Frontend polls job status.

### Async reporting flow

1. Frontend or external caller submits report parameters to `UPMS.Api`.
2. API writes `background_job` row with job type `report-execution`.
3. Worker executes the plugin from `UPMS.Reporting`.
4. Worker stores generated files in shared artifact storage when required.
5. API exposes job status and file download URL.

### External consumer flow (Power Query / BI / scripts)

1. Consumer calls `UPMS.Api` directly.
2. Consumer authenticates with API key when enabled.
3. Consumer reads structured JSON endpoints such as tickets, snapshots, ITSM sources, and job outputs.

## Implementation outcome

The repository now implements the split runtime directly.

- New logic is extracted into reusable libraries instead of staying inside the web app.
- New container defaults point to API + worker + frontend.

The active repository shape is therefore:

- `UPMS.Data` as the shared core data/platform library
- `UPMS.Ingestion` as reusable ingest orchestration
- `UPMS.Reporting` as reusable report orchestration and plugins
- `UPMS.Api` as the primary application boundary
- `UPMS.Worker` for background ingest and report jobs
- `UPMS.Frontend` as the primary CGI EDS React user interface
