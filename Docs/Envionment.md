# Development Environment

**Docker-first development environment for UPMS**

This document describes the expected development environment for the Unified Problem Management System.

---

## Core Principle

> Everything must be runnable via `docker compose up`.

- All services run in containers.
- Local development should mirror containerised execution.
- You *can* run `UPMS.Web` locally with the .NET SDK for faster iteration, but it should still point at the Docker-hosted PostgreSQL instance.

---

## Technology Stack

### Web application

- **Framework:** Blazor Server (.NET 10)
- **Language:** C#
- **Rendering model:** Interactive Server (SignalR)
- **Container:** `UPMS_web`

### Data access library

- **Type:** .NET class library (.NET 10)
- **ORM:** EF Core + Npgsql
- **Referenced by:** `UPMS.Web` as a project dependency (not a separate container)

### Database

- **Engine:** PostgreSQL 16
- **Container:** `UPMS_db`
- **Schema management:** EF Core migrations in `src/UPMS.Data/Migrations`, applied automatically by the web app at startup.
- **Notes:** The repo also contains reference SQL in `sql/stored-procedures/`.

---

## Services

| Container | Responsibility | Port (host) |
|-----------|---------------|-------------|
| `UPMS_web` | Blazor Server web app | `8081` |
| `UPMS_db` | PostgreSQL database | `5432` |
| `UPMS_db_test` | PostgreSQL database for integration tests | `5433` |

There is no separate API backend and no worker service. The Blazor Server application handles server-side logic and UI.

---

## Running the Application

### Prerequisites

- Docker Desktop (or Docker Engine with Compose v2+)
- .NET SDK (only required if you want to run the web app outside Docker)

### Start services

```bash
docker compose up -d
```

The application will be available at **http://localhost:8081**.

On startup, `UPMS_web` applies EF Core migrations automatically.

### Stop services

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

Most development defaults are defined in `docker-compose.override.yml`.

### Database (`UPMS_db`)

| Variable | Default | Description |
|----------|---------|-------------|
| `POSTGRES_USER` | `upms` | PostgreSQL username |
| `POSTGRES_PASSWORD` | `upms` | PostgreSQL password |
| `POSTGRES_DB` | `upms` | PostgreSQL database name |

### Web application (`UPMS_web`)

| Variable | Default (dev) | Description |
|----------|----------------|-------------|
| `ASPNETCORE_ENVIRONMENT` | `Development` | ASP.NET Core environment |
| `ASPNETCORE_URLS` | `http://+:8080` | Kestrel binding inside container |
| `UPMS_CONNECTION_STRING` | `Host=UPMS_db;Port=5432;Database=upms;Username=upms;Password=upms` | PostgreSQL connection string |
| `UPMS_CONNECTION_STRING_FILE` | *(unset)* | Optional: read a connection string from a file (Docker secrets style) |
| `AUTH_MODE` | `None` | Auth mode (`None` for dev, `Entra` for Azure AD) |

---

## Local Development Without Docker for the Web App

If you want to run `UPMS.Web` locally while PostgreSQL runs in Docker:

1. Start the DB container:
   ```bash
   docker compose up -d UPMS_db
   ```

2. Set your connection string (environment variable is simplest):
   ```bash
   export UPMS_CONNECTION_STRING="Host=localhost;Port=5432;Database=upms;Username=upms;Password=upms"
   ```

3. Run the web app:
   ```bash
   cd src/UPMS.Web
   dotnet run
   ```

---

## Database Lifecycle

### Migrations

- The schema is managed by EF Core migrations in `src/UPMS.Data/Migrations`.
- `UPMS_web` runs `db.Database.MigrateAsync()` on startup.

To create a new migration (requires .NET SDK + EF tooling):

```bash
cd src/UPMS.Data
dotnet ef migrations add <MigrationName> --startup-project ../UPMS.Web
```

---

## Health Checks

| Service | Health Check |
|---------|--------------|
| `UPMS_db` | `pg_isready -U upms -d upms` |
| `UPMS_web` | Depends on `UPMS_db` being healthy before starting |

---

## Optional Tools

A DB admin UI (Adminer/pgAdmin) is not included by default. If you want one, add it under a `tools` profile.
See [`Docs/DOCKER.md`](DOCKER.md).
