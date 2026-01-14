# DEV_ENVIRONMENT.md
**Docker-first development environment**

This document defines the **authoritative development environment** for the Snapshot-Based Reporting & Analysis Platform.

GitHub Copilot, contributors, and CI pipelines should assume:
- all services run in Docker containers,
- local development mirrors containerised execution,
- no component relies on host-installed databases or infrastructure.

---

## Core principle

> **Everything must be runnable via Docker.**

Developers may optionally run parts locally for debugging, but:
- Docker containers are the source of truth,
- Docker Compose defines how services interact,
- production-like behaviour should always be testable locally using containers.

---

## Technology stack (preferred)

### Backend
- **Runtime:** .NET 10 (LTS or latest stable)
- **Framework:** ASP.NET Core Web API
- **Language:** C#
- **Execution model:** containerised service
- **DB access:** Dapper or EF Core (stored-procedure first)
- **Background processing:** `IHostedService` / worker service (separate container)

### Database
- **Engine:** PostgreSQL 15+
- **Execution:** Docker container
- **Schema management:** SQL migrations (Flyway / EF migrations)
- **Primary access pattern:** stored procedures / functions
- **No direct table access from reporting code**

### Object / Artifact Storage
- **Dev:** MinIO (S3-compatible)
- **Prod:** S3 / Azure Blob / compatible service
- **Purpose:** store generated report artifacts (CSV, XLSX, PPTX, PDF)
- **Execution:** Docker container (MinIO)

### Frontend (Admin + Report Runner UI)
- **Framework:** React + TypeScript
- **Dev server:** Vite
- **Execution:** Docker container
- **Prod:** static build served via Nginx or CDN
- **API communication:** HTTP to backend container

---

## Containers & responsibilities

The Docker Compose stack must include:

| Container | Responsibility |
|---------|----------------|
| `db` | PostgreSQL database |
| `backend` | ASP.NET Core API |
| `worker` | Background job processor |
| `frontend` | Report UI + template admin |
| `minio` | Artifact storage |
| `adminer` (optional) | DB inspection UI |

Each container:
- runs a single responsibility,
- communicates via Docker networking,
- is replaceable without code changes.

---

## Docker as the default runtime

### Expectations
- All services **must start via `docker compose up`**.
- No service should require:
  - a locally installed database,
  - local blob storage,
  - hard-coded file paths outside mounted volumes.
- Environment configuration is provided via `.env`.

### Local-only execution (optional)
Developers *may* run:
- backend or frontend locally **only if** they point to Docker-hosted services.

Example:
- backend runs locally,
- PostgreSQL and MinIO still run in Docker.

This is optional and not the primary workflow.

---

## Configuration & environment variables

- All configuration must be injectable via environment variables.
- `.env.example` documents required variables.
- `.env` is used for local dev and **must not be committed**.

Examples:
- DB connection string
- MinIO endpoint and credentials
- Backend / frontend ports
- ASP.NET environment

---

## Database lifecycle (development)

### Initialisation
- Development DB may be initialised via:
  - `sql/init_schema.sql` mounted into Postgres (`docker-entrypoint-initdb.d`), or
  - migrations executed by the backend container.

### Schema changes
- Prefer migration tooling over ad-hoc SQL.
- Migrations must be runnable inside Docker.
- Dropping volumes is acceptable **only in dev**.

### Expectations
- DB schema is reproducible from scratch.
- No manual DB setup steps required outside Docker.

---

## Reporting & storage flow (environment-level)

1. Snapshots uploaded to backend container.
2. Backend writes:
   - snapshot metadata to Postgres,
   - field-level changes to Postgres.
3. Report generation:
   - backend / worker queries DB via stored procedures,
   - renderer produces artifact (CSV / PPTX / etc),
   - artifact stored in MinIO,
   - backend returns signed download URL.
4. Frontend consumes API responses only — never touches storage directly.

---

## Template handling (environment context)

- Templates (PPTX, XLSX, HTML) are:
  - uploaded via admin UI,
  - stored in object storage (MinIO),
  - versioned and referenced by metadata.
- Renderers load templates from storage, not from local disk paths.
- Template files are **data**, not code.

---

## Health & readiness

Each container should expose:
- a health endpoint (or healthcheck command),
- readiness for dependent services.

Examples:
- Postgres: `pg_isready`
- Backend: `/health`
- MinIO: `/minio/health/live`

Docker Compose should use healthchecks to control startup order.

---

## Logging & diagnostics

- Logs must be written to stdout/stderr.
- Docker logs are the primary debugging surface.
- Structured logging preferred (JSON or key-value).
- No reliance on local log files.

---

## CI / test environment alignment

CI pipelines should:
1. Start the full Docker Compose stack.
2. Wait for healthchecks.
3. Run integration tests against running containers.
4. Tear down containers and volumes.

There should be **no separate CI-only environment logic**.

---

## Production parity notes

While this document focuses on development:
- Production should reuse the same container images.
- Differences should be limited to:
  - managed DB instead of local Postgres,
  - managed object storage instead of MinIO,
  - static frontend hosting instead of Vite dev server.
- No code changes should be required to switch environments.

---

## Non-goals (environment)

This environment does **not** aim to:
- optimise for minimal container count,
- support non-Docker execution paths,
- embed secrets directly in files,
- run live ITSM integrations.

---

## Summary (for Copilot context)

- Docker Compose is the **authoritative runtime**.
- PostgreSQL + stored procedures are the **only data access path**.
- MinIO is used for artifact storage.
- Backend, worker, and frontend are **separate containers**.
- Templates are external assets, not code.
- All configuration is environment-driven.
- Local execution is optional; container execution is required.

Copilot should assume all new components are:
- containerised,
- environment-variable configured,
- networked via Docker,
- and compatible with this stack by default.
