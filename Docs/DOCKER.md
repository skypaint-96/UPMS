# Docker Setup — UPMS

This document describes the Docker Compose setup for UPMS: the services defined, how they are built, and how to work with them.

---

## Services

UPMS uses two containers:

| Service      | Description                          | Host Port           |
|--------------|--------------------------------------|---------------------|
| `UPMS_db`    | PostgreSQL 16 database               | `5432`              |
| `UPMS_web`   | Blazor Server web application (.NET) | `8081` (HTTP)       |

There is no worker service, no object storage, and no separate API backend. The Blazor Server application is the only application container.

---

## Compose Files

### [`docker-compose.yml`](../docker-compose.yml)

The base compose file defines the two services and the persistent database volume:

```yaml
services:
  UPMS_web:
    image: ${DOCKER_REGISTRY-}upmsweb
    build:
      context: .
      dockerfile: src/UPMS.Web/Dockerfile
    ports:
      - "8081:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:8080
      - AUTH_MODE=Entra
      - UPMS_CONNECTION_STRING_FILE=/run/secrets/upms_connection_string
    volumes:
      - upms_keys:/app/keys
    depends_on:
      UPMS_db:
        condition: service_healthy

  UPMS_db:
    image: postgres:16-alpine
    ports:
      - "5432:5432"
    environment:
      - POSTGRES_USER=upms
      - POSTGRES_DB=upms
    volumes:
      - pgdata:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U upms -d upms"]
      interval: 5s
      timeout: 5s
      retries: 10
      start_period: 10s

volumes:
  pgdata:
  upms_keys:
```

### [`docker-compose.override.yml`](../docker-compose.override.yml)

The override file applies development-specific settings — ASP.NET Core environment, port bindings, and volume mounts for .NET user secrets and HTTPS certificates:

```yaml
services:
  UPMS_web:
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ASPNETCORE_URLS=http://+:8080
      - AUTH_MODE=None
      - UPMS_CONNECTION_STRING=Host=UPMS_db;Port=5432;Database=upms;Username=upms;Password=upms
    ports:
      - "8081:8080"
    volumes:
      - ./src/UPMS.Web:/app/src/UPMS.Web:ro

  UPMS_db:
    environment:
      - POSTGRES_USER=upms
      - POSTGRES_PASSWORD=upms
      - POSTGRES_DB=upms
```

Docker Compose automatically merges both files when running `docker compose` commands in development.

---

## UPMS.Web Dockerfile

The [`src/UPMS.Web/Dockerfile`](../src/UPMS.Web/Dockerfile) uses a standard multi-stage .NET build:

| Stage    | Base Image                              | Purpose                                   |
|----------|----------------------------------------|-------------------------------------------|
| `build`  | `mcr.microsoft.com/dotnet/sdk:10.0`    | Restores and builds the project          |
| `final`  | `mcr.microsoft.com/dotnet/aspnet:10.0` | Copies published output and sets entrypoint |

The entrypoint is `dotnet UPMS.Web.dll`.

---

## Scenarios

### 1. Start the Project (Persisting Existing Data)

To start the project while keeping the existing database volume intact:

```bash
docker compose up -d
```

This command starts all services defined in the `docker-compose.yml` and `docker-compose.override.yml` files. The database volume (`pgdata`) is preserved, ensuring existing data remains intact.

### 2. Stop the Service

To stop the services without removing containers or data:

```bash
docker compose stop
```

To stop and remove containers (but keep volumes):

```bash
docker compose down
```

### 3. Full Fresh Start — Persisting Data

To rebuild the application image and restart services while keeping the existing database data:

```bash
docker compose up --build -d
```

This command rebuilds the `UPMS_web` image and restarts all services, preserving the database volume.

### 4. Full Fresh Start — Clean Slate

To rebuild the application image and wipe the database:

```bash
docker compose down -v
```

```bash
docker compose up --build -d
```

The `-v` flag removes all volumes, including the database volume (`pgdata`). On the next startup, the database will be reinitialized from EF Core migrations.

---

## Common Commands

### View Logs

```bash
docker compose logs -f UPMS_web
docker compose logs -f UPMS_db
```

### Connect to the Database

```bash
docker compose exec UPMS_db psql -U upms -d upms
```

### Reset the Database (Destroys All Data)

```bash
docker compose down -v
docker compose up -d
```

---

## Connection String

The `UPMS_web` container connects to the `UPMS_db` container using the Docker service name as the hostname:

```
Host=UPMS_db;Port=5432;Database=upms;Username=upms;Password=upms
```

This is configured via the `UPMS_CONNECTION_STRING` environment variable in development or `UPMS_CONNECTION_STRING_FILE` in production.

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
    - UPMS_db
```

Start with:

```bash
docker compose --profile tools up -d
```

Access at **http://localhost:8090** — connect with system `PostgreSQL`, server `UPMS_db`, username `upms`, password `upms`, database `upms`.
