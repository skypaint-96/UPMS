# Docker Setup — UPMS

## Default service layout

The default compose layout now runs four services:

| Service | Description | Host Port |
|---------|-------------|-----------|
| `UPMS_frontend` | React 19 + CGI EDS frontend served by Nginx | `8080` |
| `UPMS_api` | `.NET 10` API | `8081` |
| `UPMS_worker` | `.NET 10` background worker | none |
| `UPMS_db` | PostgreSQL 16 | `5432` |

## Shared storage

`UPMS_api` and `UPMS_worker` share the `upms_artifacts` volume mounted at `/var/lib/upms`.

It stores:

- uploaded snapshot files
- report template files
- generated report artifacts

## Start the stack

```bash
docker compose up --build -d
```

## Useful URLs

- Frontend: `http://localhost:8080`
- OpenAPI: `http://localhost:8081/openapi/v1.json`
- API base: `http://localhost:8081/api/v1`

## Development defaults

The override file switches the API to `Auth:Mode=None` and keeps the database password simple for local work.

## Notes

- The frontend proxies `/api/*` to `UPMS_api` internally.
- External tools such as Excel Power Query can also call the API directly on `8081`.
- API/worker both rely on the shared PostgreSQL connection string and artifact volume.
