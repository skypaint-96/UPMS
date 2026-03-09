# Container Hosting and Packaging

## Default runtime shape

The default compose-based runtime now targets four services:

- PostgreSQL database
- `UPMS.Api`
- `UPMS.Worker`
- `UPMS.Frontend`

## Shared storage contract

A shared artifact volume is mounted for both API and worker. It is used for:

- uploaded snapshot files
- generated report files
- template/report storage

## Dockerfiles included

- `src/UPMS.Api/Dockerfile`
- `src/UPMS.Worker/Dockerfile`
- `src/UPMS.Frontend/Dockerfile`

## Local developer expectations

### Backend
A local developer should use a `.NET 10` SDK.

### Frontend
A local developer should use:

- Node `20.x`
- Yarn `1.22.19`

### Containers
The default compose entry point should be the frontend, with API available both directly and through the frontend proxy.
