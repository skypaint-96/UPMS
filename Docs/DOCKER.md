# Docker Setup — UPMS

## Default service layout

The default compose layout runs four services:

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

For a full reset on a development machine:

```bash
docker compose down --remove-orphans -v
docker compose build --no-cache
docker compose up -d
```

## Useful URLs

- Frontend: `http://localhost:8080`
- OpenAPI: `http://localhost:8081/openapi/v1.json`
- API base: `http://localhost:8081/api/v1`

## Configuration defaults

The base compose file is runnable locally without extra setup.

- `UPMS_DB_PASSWORD` defaults to `upms`
- `UPMS_API_KEY` defaults to `change-me`
- `docker-compose.override.yml` switches the API to `ASPNETCORE_ENVIRONMENT=Development` and disables auth for local work

You can still override either value from the shell or a local `.env` file when you need something different.

## Notes

- The frontend proxies `/api/*` to `UPMS_api` internally.
- External tools such as Excel Power Query can also call the API directly on `8081`.
- API and worker both rely on the shared PostgreSQL connection string and artifact volume.
- `node:20-alpine` already ships a `yarn` executable path. The frontend Dockerfile therefore uses `npm install -g yarn@1.22.19 --force` so the required Yarn version replaces the pre-existing shim instead of failing with `EEXIST`.

## Troubleshooting

If Compose reports that the default network is still in use, remove orphaned containers first:

```bash
docker compose down --remove-orphans -v
```
