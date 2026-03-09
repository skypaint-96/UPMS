# React Frontend UI Map

## Route map

| Route | Purpose |
|------|---------|
| `/` | overview/dashboard |
| `/snapshots` | snapshot browsing |
| `/tickets` | ticket query/search |
| `/tickets/:company/:ticketKey` | ticket detail + history |
| `/itsm-sources` | source definitions + mappings |
| `/upload` | queue ingest jobs |
| `/reports` | queue report jobs |
| `/jobs` | monitor jobs and download results |

## UI composition

- `CGITopNavigation` drives the global shell header
- `CGILeftNavigation` drives the main nav rail
- `CGIDataTable` renders the primary data-heavy views
- `CGIAlert` renders error messaging
- `edsTheme` is used as the MUI theme base
