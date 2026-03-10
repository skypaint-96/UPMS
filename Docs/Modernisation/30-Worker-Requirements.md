# Worker Requirements

## Functional requirements

### WK-001 — Queue-backed processing
The worker shall process database-backed background jobs instead of relying on in-memory task execution.

### WK-002 — Snapshot ingest job processing
The worker shall process queued snapshot-ingest jobs using the shared ingest library.

### WK-003 — Report execution job processing
The worker shall process queued report-execution jobs using the shared reporting library.

### WK-004 — Shared artifact handling
The worker shall read uploaded files from shared storage and write generated report artifacts back to shared storage.

### WK-005 — Job status lifecycle
The worker shall update jobs through `pending -> running -> succeeded/failed` states.

### WK-006 — Resilience for long-running work
The worker shall use renewable job leases to reduce the chance of duplicate processing after worker interruption.

## Non-functional requirements

### WK-101 — `.NET 10`
The worker shall target `.NET 10`.

### WK-102 — Container-first deployment
The worker shall be independently containerized and able to run alongside the API.
