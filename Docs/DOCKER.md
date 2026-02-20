# Docker Setup — UPMS

This document describes the Docker Compose setup for UPMS: the services defined, how they are built, and how to work with them.

---

## Services

UPMS uses two containers:

| Service | Description | Host Port |
|---------|-------------|-----------|
| `db` | PostgreSQL 16 database | `5432` |
| `upms.web` | Blazor Server web application (.NET 10) | `8080` (HTTP), `8081` (HTTPS) |

There is no worker service, no object storage, and no separate API backend. The Blazor Server application is the only application container.

---

## Compose Files

### [`docker-compose.yml`](../docker-compose.yml)

The base compose file defines the two services and the persistent database volume:

```yaml
services:
  upms.web:
    image: ${DOCKER_REGISTRY-}upmsweb
    build:
      context: .
      dockerfile: src/UPMS.Web/Dockerfile

  db:
    image: postgres:16
    environment:
      POSTGRES_USER: upms
      POSTGRES_PASSWORD: upms
      POSTGRES_DB: upms
    ports:
      - "5432:5432"
    volumes:
      - pgdata:/var/lib/postgresql/data

volumes:
  pgdata:
```

### [`docker-compose.override.yml`](../docker-compose.override.yml)

The override file applies development-specific settings — ASP.NET Core environment, port bindings, and volume mounts for .NET user secrets and HTTPS certificates:

```yaml
services:
  upms.web:
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ASPNETCORE_HTTP_PORTS=8080
      - ASPNETCORE_HTTPS_PORTS=8081
    ports:
      - "8080"
      - "8081"
    volumes:
      - ${APPDATA}/Microsoft/UserSecrets:/home/app/.microsoft/usersecrets:ro
      - ${APPDATA}/ASP.NET/Https:/home/app/.aspnet/https:ro
```

Docker Compose automatically merges both files when running `docker compose` commands in development.

---

## UPMS.Web Dockerfile

The [`src/UPMS.Web/Dockerfile`](../src/UPMS.Web/Dockerfile) uses a standard multi-stage .NET build:

| Stage | Base Image | Purpose |
|-------|-----------|---------|
| `base` | `mcr.microsoft.com/dotnet/aspnet:10.0` | Runtime image — exposes ports 8080 and 8081 |
| `build` | `mcr.microsoft.com/dotnet/sdk:10.0` | Restores and builds the project |
| `publish` | `build` | Publishes the self-contained app to `/app/publish` |
| `final` | `base` | Copies published output and sets entrypoint |

The entrypoint is `dotnet UPMS.Web.dll`.

`UPMS.Data` is a referenced class library — it is compiled into `UPMS.Web` and does not have its own container or Dockerfile.

---

## Database Initialisation

The `db` container uses a named Docker volume (`pgdata`) for persistence. On first startup (when the volume is empty), PostgreSQL will run any `.sql` files mounted at `/docker-entrypoint-initdb.d/`.

To wire up the migration scripts for automatic initialisation, mount the migrations directory in the compose file:

```yaml
db:
  volumes:
    - pgdata:/var/lib/postgresql/data
    - ./sql/migrations:/docker-entrypoint-initdb.d:ro
```

> **Note:** The current `docker-compose.yml` does not yet mount the migrations directory. Migrations must be applied manually or this mount must be added. See [`sql/migrations/`](../sql/migrations/) for the migration scripts.

To apply migrations manually against the running container:

```bash
docker compose exec db psql -U upms -d upms -f /path/to/migration.sql
```

Or from the host, piping the file in:

```bash
docker compose exec -T db psql -U upms -d upms < sql/migrations/001_initial_schema.sql
docker compose exec -T db psql -U upms -d upms < sql/migrations/002_indexes.sql
```

---

## Common Commands

### Start all services

```bash
docker compose up -d
```

Application: **http://localhost:8080**

### View logs

```bash
docker compose logs -f upms.web
docker compose logs -f db
```

### Rebuild the web application

```bash
docker compose build upms.web
docker compose up -d upms.web
```

### Connect to the database

```bash
docker compose exec db psql -U upms -d upms
```

### Reset the database (destroys all data)

```bash
docker compose down -v
docker compose up -d
```

---

## Connection String

The `upms.web` container connects to the `db` container using the Docker service name as the hostname:

```
Host=db;Port=5432;Database=upms;Username=upms;Password=upms
```

This is configured via the `ConnectionStrings__Default` environment variable (or `appsettings.json` for local development using `Host=localhost`).

---

## Database Admin UI (Optional)

Adminer is not included in the base compose file but can be added under a `tools` profile for local DB inspection:

```yaml
adminer:
  image: adminer:latest
  ports:
    - "8090:8080"
  profiles:
    - tools
  depends_on:
    - db
```

Start with:
```bash
docker compose --profile tools up -d
```

Access at **http://localhost:8090** — connect with system `PostgreSQL`, server `db`, username `upms`, password `upms`, database `upms`.
