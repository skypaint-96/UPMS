# Docker & Compose Setup for UPMS

## Services
- **upms.web**: Blazor frontend
- **upms.data**: Data service (library, migrations, or background tasks)
- **upms.data.tests**: Runs integration/unit tests
- **db**: PostgreSQL database (persistent volume)

## Usage

### Build all services
```
docker compose build
```

### Start all services (web, data, db)
```
docker compose up
```

### Run tests
```
docker compose run --rm upms.data.tests
```

## Environment
- Data and test services use `ConnectionStrings__Default=Host=db;Port=5432;Database=upms;Username=upms;Password=upms`
- Database data is persisted in the `pgdata` Docker volume

## Notes
- Adjust Dockerfiles if you add migrations or CLI tools to UPMS.Data
- For local development, you may want to use user secrets or override connection strings
