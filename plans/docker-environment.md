# Docker Environment — Architecture Note

> **⚠️ Superseded**
>
> This document was originally written to plan a multi-container architecture consisting of a React/Vite frontend, an ASP.NET Core Web API backend, a .NET Worker Service, and MinIO object storage. **That architecture was not adopted.**
>
> This file is retained for historical reference only. The content below reflects the current, simplified architecture.

---

## Current Docker Architecture

UPMS uses a two-container Docker Compose setup:

```
┌─────────────────────────────────────┐
│         Docker Compose Stack        │
│                                     │
│  ┌─────────────┐   ┌─────────────┐  │
│  │  upms.web   │──▶│     db      │  │
│  │ Blazor Server│   │ PostgreSQL  │  │
│  │   :8080     │   │   :5432     │  │
│  └─────────────┘   └─────────────┘  │
└─────────────────────────────────────┘
```

| Container | Image / Build | Purpose |
|-----------|--------------|---------|
| `upms.web` | Built from [`src/UPMS.Web/Dockerfile`](../src/UPMS.Web/Dockerfile) (.NET 10) | Blazor Server web application — handles all UI and server-side logic |
| `db` | `postgres:16` | PostgreSQL 16 database — persisted via `pgdata` named volume |

`UPMS.Data` is a class library compiled into `upms.web`. It is not a separate container.

### What Was Removed

The original plan included the following containers, which **do not exist** in the current implementation:

- ❌ `frontend` — React + TypeScript + Vite SPA
- ❌ `backend` — ASP.NET Core Web API
- ❌ `worker` — .NET Worker Service
- ❌ `minio` — S3-compatible object storage

---

## Authoritative Documentation

For the current development environment specification, see:

- [`Docs/Envionment.md`](../Docs/Envionment.md) — full environment specification
- [`Docs/DOCKER.md`](../Docs/DOCKER.md) — Docker Compose and Dockerfile details
