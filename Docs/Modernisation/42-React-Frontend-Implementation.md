# React Frontend Implementation

## Implemented project

- `src/UPMS.Frontend/`

## Key implementation choices

### CGI EDS as the shell foundation
The React app uses CGI EDS for:

- top navigation
- left navigation
- data-table surfaces
- alert styling
- theme integration

### API client abstraction
A small `axios` client wraps the `/api/v1` routes for:

- canonical fields
- ITSM sources
- snapshots
- tickets
- ticket history
- report plugins
- background jobs
- ingest/report job submission

### Page coverage included
The implementation includes pages for:

- dashboard
- snapshots
- tickets
- ticket detail
- ITSM sources
- upload
- reports
- jobs

### Deployment assumptions
The production frontend container serves the built SPA via Nginx and proxies `/api/` to the API container.
