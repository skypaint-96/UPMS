# UPMS retention, restore, and canonical-field changes

This patch adds the following capabilities:

- Snapshot export and hard delete.
- ITSM source export, hard delete, and import/restore.
- Editable canonical field registry stored in the database, including datatype metadata.
- Preservation of original source field names in `field_change.field_name`.
- Separate `field_change.canonical_field_name` for display and validation.
- Canonical datatype validation in ticket history/current-field views.
- Startup schema bootstrap for `canonical_field_definition` and `field_change.canonical_field_name`.

## Main touchpoints

- `src/UPMS.Web/Services/UpmsArchiveService.cs`
- `src/UPMS.Web/Services/SnapshotIngestService.cs`
- `src/UPMS.Data/CanonicalField*.cs`
- `src/UPMS.Data/UpmsSchemaBootstrapper.cs`
- `src/UPMS.Data/TicketDataService.cs`
- `src/UPMS.Web/Components/Pages/CanonicalFields.razor`
- `src/UPMS.Web/Components/Pages/ItsmSources.razor`
- `src/UPMS.Web/Components/Pages/ItsmSourceDetail.razor`
- `src/UPMS.Web/Components/Pages/Snapshots.razor`
- `src/UPMS.Web/Components/Pages/SnapshotDetail.razor`
- `src/UPMS.Web/Components/Pages/TicketDetail.razor`

## Operational note

The database schema additions are applied at application startup by `UpmsSchemaBootstrapper` after EF migrations run. No new EF migration file was generated in this environment.

## Validation note

A full `dotnet build` could not be run in this container because the .NET SDK is not installed here.
