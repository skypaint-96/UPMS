# Worker Implementation

## Implemented project

- `src/UPMS.Worker/`

## Implemented behaviour

### Background job host
A dedicated `BackgroundService` named `BackgroundJobWorker` now:

- polls for supported jobs
- claims one job at a time
- dispatches by job type
- marks jobs succeeded or failed

### Supported job types

- `snapshot-ingest`
- `report-execution`

### Artifact storage integration
The worker uses the same configured artifact root as the API (`Artifacts:RootPath`), allowing both containers to share:

- uploaded source files
- generated report files

### Result model
For non-file reports, the worker stores preview/result JSON on the job row.
For file outputs, the worker stores:

- artifact relative path
- output file name
- output content type

The API then exposes the download endpoint.
