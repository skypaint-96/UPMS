# Development Environment

**Docker-first development environment for UPMS**

This document defines the authoritative development environment for the Unified Problem Management System. All contributors and CI pipelines should assume this specification.

---

## Core Principle

> **Everything must be runnable via `docker compose up`.**

- All services run in Docker containers.
- Local development mirrors containerised execution.
- No component relies on a host-installed database or any external infrastructure.

Developers may optionally run `UPMS.Web` locally with the .NET SDK for faster iteration, but must point it at the Docker-hosted PostgreSQL instance.

---

## Technology Stack

### Web Application
- **Framework:** Blazor Server (.NET 10)
- **Language:** C#
- **Rendering model:** Interactive Server (SignalR-based)
- **Container:** single `upms.web` container

### Data Access Library
- **Type:** .NET class library (.NET 10)
- **ORM:** Dapper + Npgsql
- **Referenced by:** `UPMS.Web` as a project dependency (not a separate container)

### Database
- **Engine:** PostgreSQL 15+
- **Container:** `db`
- **Schema management:** SQL migration scripts in [`sql/migrations/`](../sql/migrations/), applied on container initialisation via `docker-entrypoint-initdb.d`
- **Primary access pattern:** stored procedures for bulk reads; direct Dapper queries for writes

---

## Services

| Container | Responsibility | Port (host) |
|-----------|---------------|-------------|
| `db` | PostgreSQL 15 database | `5432` |
| `upms.web` | Blazor Server web application | `8080` |
| `adminer` *(optional)* | Database inspection UI | `8090` |

There is no worker service, no message queue, no object storage (MinIO or otherwise), and no separate API backend. The Blazor Server application handles all server-side logic and UI.

---

## Running the Application

### Prerequisites
- Docker Desktop (or Docker Engine with Compose v2+)
- .NET 10 SDK *(only required for local non-Docker development)*

### Start all services

```bash
docker compose up -d
```

The application will be available at: **http://localhost:8080**

### Start with the database admin UI

```bash
docker compose --profile tools up -d
```

Adminer will be available at: **http://localhost:8090**
- System: `PostgreSQL`
- Server: `db`
- Username: `upms`
- Password: `upms_dev_password`
- Database: `upms`

### Stop all services

```bash
docker compose down
```

### Reset the database (destroys all data)

```bash
docker compose down -v
docker compose up -d
```

---

## Environment Variables

Configuration is provided via environment variables. For local development these are set in `docker-compose.override.yml` or a `.env` file (not committed to source control).

### Database

| Variable | Default | Description |
|----------|---------|-------------|
| `POSTGRES_USER` | `upms` | PostgreSQL username |
| `POSTGRES_PASSWORD` | `upms_dev_password` | PostgreSQL password |
| `POSTGRES_DB` | `upms` | PostgreSQL database name |

### Application (`upms.web`)

| Variable | Default | Description |
|----------|---------|-------------|
| `ASPNETCORE_ENVIRONMENT` | `Development` | ASP.NET Core environment name |
| `ConnectionStrings__Default` | `Host=db;Port=5432;Database=upms;Username=upms;Password=upms_dev_password` | PostgreSQL connection string |

### Connection String Convention

The connection string is set via the `ConnectionStrings__Default` environment variable (double underscore = nested JSON key separator in .NET configuration). It maps to the `ConnectionStrings.Default` key in `appsettings.json`.

Example:
```
ConnectionStrings__Default=Host=db;Port=5432;Database=upms;Username=upms;Password=upms_dev_password
```

For local (non-Docker) development, use `Host=localhost` instead of `Host=db`.

---

## Database Lifecycle

### Initialisation

SQL migration scripts in [`sql/migrations/`](../sql/migrations/) are mounted into the PostgreSQL container at `/docker-entrypoint-initdb.d/`. PostgreSQL runs all `.sql` files in that directory automatically on first startup (i.e. when the `pgdata` volume is empty).

Scripts are run in filename order:
1. [`001_initial_schema.sql`](../sql/migrations/001_initial_schema.sql) — creates tables
2. [`002_indexes.sql`](../sql/migrations/002_indexes.sql) — creates indexes

Stored procedures in [`sql/stored-procedures/`](../sql/stored-procedures/) must be applied separately if needed for local development.

### Schema Changes

When a new migration is needed:
1. Add a new numbered `.sql` file to [`sql/migrations/`](../sql/migrations/).
2. Run `docker compose down -v && docker compose up -d` to recreate the database from scratch.

In development, destroying and recreating the volume is the expected workflow for schema changes.

---

## Local Development (Without Docker for the Web App)

If running `UPMS.Web` locally with the .NET SDK while PostgreSQL runs in Docker:

1. Start the database container:
   ```bash
   docker compose up -d db
   ```

2. Set the connection string in `src/UPMS.Web/appsettings.Development.json` or via user secrets:
   ```json
   {
     "ConnectionStrings": {
       "Default": "Host=localhost;Port=5432;Database=upms;Username=upms;Password=upms_dev_password"
     }
   }
   ```

3. Run the application:
   ```bash
   cd src/UPMS.Web
   dotnet run
   ```

---

## Health Checks

| Service | Health Check |
|---------|-------------|
| `db` | `pg_isready -U upms -d upms` |
| `upms.web` | Depends on `db` being healthy before starting |

---

## Logging

- All application logs are written to stdout/stderr.
- `docker compose logs -f upms.web` is the primary debugging surface.
- Structured logging is preferred (ASP.NET Core default JSON format in production).

---

## Non-Goals

This environment does **not** include:
- A React, TypeScript, or Vite frontend
- A separate ASP.NET Core Web API backend
- A .NET Worker Service container
- MinIO or any object storage
- Live ITSM system integrations
