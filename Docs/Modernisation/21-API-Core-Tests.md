# API Core Test Plan

Each test maps back to one or more API requirements.

## Contract tests

- **AT-001**: `GET /api/v1/health` returns `200 OK` and a service/status payload. (`AP-001`)
- **AT-002**: `GET /api/v1/canonical-fields` returns canonical fields including `Number` and `Company`. (`AP-002`)
- **AT-003**: `GET /api/v1/itsm-sources` returns source summaries. (`AP-003`)
- **AT-004**: `GET /api/v1/itsm-sources/{name}` returns source mappings. (`AP-003`, `AP-004`)
- **AT-005**: `GET /api/v1/snapshots` supports source/company filters. (`AP-005`)
- **AT-006**: `GET /api/v1/snapshots/{id}/tickets` returns snapshot ticket roster. (`AP-006`)
- **AT-007**: `GET /api/v1/tickets` reconstructs tickets as-of time. (`AP-007`)
- **AT-008**: `GET /api/v1/tickets/{company}/{ticketKey}` returns ticket detail. (`AP-008`)
- **AT-009**: `GET /api/v1/tickets/{company}/{ticketKey}/history` returns field history. (`AP-009`)
- **AT-010**: `POST /api/v1/snapshots/ingest` accepts multipart upload and returns ingest summary. (`AP-010`)
- **AT-011**: `GET /api/v1/reports/plugins` returns plugin metadata. (`AP-011`)
- **AT-012**: `POST /api/v1/reports/execute` returns either preview JSON or a file response. (`AP-012`)

## Security tests

- **AT-101**: with `Auth:Mode=None`, functional endpoints are available without API key. (`AP-104`)
- **AT-102**: with `Auth:Mode=ApiKey`, protected endpoints reject missing/invalid API keys. (`AP-104`)
- **AT-103**: with valid API key, protected endpoints succeed. (`AP-104`)

## Packaging tests

- **AT-201**: API container builds successfully using the repo Dockerfile. (`AP-105`)
- **AT-202**: API starts in compose and applies relational migrations when using PostgreSQL. (`AP-105`)
