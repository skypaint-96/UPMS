# React Frontend Requirements

## Platform constraints applied

The frontend in this implementation is aligned to the package constraints supplied for CGI EDS:

- React `19.0.0`
- MUI `5.15.14`
- Node `20.x`
- Yarn `1.22.19`

## Functional requirements

### FE-001 — Frontend becomes the primary UI
The React app shall provide the primary user experience for the new architecture.

### FE-002 — CGI EDS alignment
The frontend shall use the CGI EDS package as its UI foundation.

### FE-003 — API-first integration
The frontend shall retrieve data exclusively from `UPMS.Api`.

### FE-004 — Ticket browsing workflows
The frontend shall support ticket search, detail, and history views.

### FE-005 — Snapshot browsing workflows
The frontend shall support snapshot browsing.

### FE-006 — ITSM source browsing
The frontend shall support viewing ITSM source definitions and mappings.

### FE-007 — Upload workflow
The frontend shall support queueing snapshot ingest jobs.

### FE-008 — Reporting workflow
The frontend shall support queueing report jobs and collecting outputs.

### FE-009 — Job monitoring
The frontend shall expose background-job state and downloads/previews.

## Non-functional requirements

### FE-101 — SPA hosting
The frontend shall be packaged as a separately hosted container.

### FE-102 — Same-origin API convenience
The frontend container shall proxy `/api` requests to the API container by default.
