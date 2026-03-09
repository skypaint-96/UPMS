# Worker Test Plan

- **WT-001**: pending snapshot-ingest job is claimed and transitioned to running. (`WK-001`, `WK-005`)
- **WT-002**: snapshot-ingest job deserializes payload, opens stored file, and executes ingest service. (`WK-002`, `WK-004`)
- **WT-003**: report-execution job deserializes payload and executes the selected report plugin. (`WK-003`)
- **WT-004**: file-producing report job stores artifact metadata and download path. (`WK-003`, `WK-004`)
- **WT-005**: failing job is marked failed with a captured error message. (`WK-005`)
- **WT-006**: expired running lease can be reclaimed. (`WK-006`)
- **WT-007**: worker container starts with the same shared storage root as the API. (`WK-102`)
