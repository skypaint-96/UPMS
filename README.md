# UPMS - Snapshot-Based Reporting & Analysis Platform

A flexible reporting platform that accurately shows how ITSM tickets looked at any point in time, using snapshot data exported from multiple ITSM systems.

## Quick Start

### Prerequisites
- Docker Desktop or Docker Engine with Compose v2+
- Git

### Setup

1. **Clone the repository**
   ```bash
   git clone <repository-url>
   cd UPMS
   ```

2. **Create environment file**
   ```bash
   cp .env.example .env
   ```

3. **Start all services**
   ```bash
   docker compose up -d
   ```

4. **Access the application**
   - Frontend: http://localhost:3000
   - Backend API: http://localhost:5000
   - MinIO Console: http://localhost:9001

### Start with Database Admin UI
```bash
docker compose --profile tools up -d
```
- Adminer: http://localhost:8080

---

## Project Structure

```
UPMS/
├── docker-compose.yml          # Main orchestration file
├── .env.example                # Environment variable template
├── .env                        # Local environment - NOT committed
│
├── src/
│   ├── Backend/                # ASP.NET Core Web API
│   │   ├── Backend.csproj
│   │   ├── Program.cs
│   │   ├── Controllers/        # API endpoints
│   │   ├── Services/           # Business logic
│   │   └── appsettings.json
│   │
│   ├── Worker/                 # .NET Worker Service
│   │   ├── Worker.csproj
│   │   ├── Program.cs
│   │   └── Workers/            # Background job processors
│   │
│   └── Frontend/               # React + TypeScript + Vite
│       ├── package.json
│       ├── vite.config.ts
│       ├── tsconfig.json
│       ├── index.html
│       └── src/
│           ├── main.tsx
│           ├── App.tsx
│           └── components/
│
├── sql/
│   ├── migrations/             # Database migrations
│   │   ├── 001_create_raw_snapshot.sql
│   │   ├── 002_create_snapshot_ticket.sql
│   │   ├── 003_create_field_change.sql
│   │   └── 004_create_indexes.sql
│   │
│   └── stored-procedures/      # Stored procedures
│       ├── get_tickets_as_of.sql
│       ├── get_fields_for_ticket.sql
│       └── batch_reconstruct.sql
│
├── docker/
│   ├── backend/
│   │   └── Dockerfile          # Backend container definition
│   ├── worker/
│   │   └── Dockerfile          # Worker container definition
│   └── frontend/
│       └── Dockerfile          # Frontend dev container definition
│
├── plans/                      # Architecture and planning docs
│
└── Docs/                       # Project documentation
    ├── Project Overview.md
    ├── Technical Architecture.md
    ├── Environment.md
    └── Build Stages.md
```

---

## Services

| Service | Description | Port |
|---------|-------------|------|
| `db` | PostgreSQL 15 database | 5432 |
| `minio` | S3-compatible object storage | 9000 (API), 9001 (Console) |
| `backend` | ASP.NET Core Web API | 5000 |
| `worker` | Background job processor | - |
| `frontend` | React + Vite dev server | 3000 |
| `adminer` | Database admin UI (optional) | 8080 |

---

## Common Commands

### Service Management
```bash
# Start all services
docker compose up -d

# Stop all services
docker compose down

# View logs
docker compose logs -f [service-name]

# Rebuild a service
docker compose build [service-name]
docker compose up -d [service-name]
```

### Database Operations
```bash
# Connect to PostgreSQL
docker compose exec db psql -U upms -d upms

# Reset database (WARNING: destroys all data)
docker compose down -v
docker compose up -d
```

### Development
```bash
# Rebuild and restart backend
docker compose build backend && docker compose up -d backend

# Rebuild and restart frontend
docker compose build frontend && docker compose up -d frontend

# View backend logs
docker compose logs -f backend

# View worker logs
docker compose logs -f worker
```

---

## Environment Variables

All configuration is managed via environment variables. See `.env.example` for the complete list.

### Key Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `POSTGRES_USER` | `upms` | Database username |
| `POSTGRES_PASSWORD` | `upms_dev_password` | Database password |
| `POSTGRES_DB` | `upms` | Database name |
| `MINIO_ROOT_USER` | `minioadmin` | MinIO root username |
| `MINIO_ROOT_PASSWORD` | `minioadmin123` | MinIO root password |
| `BACKEND_PORT` | `5000` | Backend API port |
| `FRONTEND_PORT` | `3000` | Frontend dev server port |

---

## Architecture

### Container Communication

```
┌─────────────────────────────────────────────────────────────┐
│                    Docker Network: upms-network              │
│                                                              │
│  ┌──────────┐    ┌──────────┐    ┌──────────┐               │
│  │ Frontend │───▶│ Backend  │───▶│ PostgreSQL│              │
│  │  :3000   │    │  :5000   │    │  :5432    │              │
│  └──────────┘    └────┬─────┘    └──────────┘               │
│                       │                                      │
│                       ▼                                      │
│  ┌──────────┐    ┌──────────┐                               │
│  │  Worker  │───▶│  MinIO   │                               │
│  │          │    │:9000/9001│                               │
│  └──────────┘    └──────────┘                               │
└─────────────────────────────────────────────────────────────┘
```

### Health Checks

All services implement health checks to ensure proper startup order:

- **PostgreSQL**: `pg_isready`
- **MinIO**: `mc ready local`
- **Backend**: `GET /health`

---

## Documentation

- [`Docs/Project Overview.md`](Docs/Project%20Overview.md) - Non-technical project overview
- [`Docs/Technical Architecture.md`](Docs/Technical%20Architecture.md) - System architecture details
- [`Docs/Environment.md`](Docs/Envionment.md) - Development environment specification
- [`Docs/Build Stages.md`](Docs/Build%20Stages.md) - Build stages and implementation order
- [`plans/docker-environment.md`](plans/docker-environment.md) - Docker environment design

---

## License

See [LICENSE](LICENSE) for details.
