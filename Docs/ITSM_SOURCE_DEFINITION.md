# ITSM Source Definition — Reference Guide

---

## Overview

An **ITSM Source** in UPMS is a **named, configured instance** of a ticketing tool. It is not a generic tool type label — it is a specific deployment or environment of an ITSM system, uniquely identified by a slug-style name.

### Why Named Instances?

Two separate ServiceNow environments used by the same team (for example, one for a client and one for internal use) are **two different ITSM sources** in UPMS. They may export different column names from their ticket tables even though both are ServiceNow. By treating each deployment as its own named source, UPMS can hold a distinct set of field mappings for each one, with no ambiguity between them.

Examples of ITSM source names:

| Name (slug) | Display Label | Notes |
|-------------|---------------|-------|
| `servicenow-client-a` | ServiceNow — Client A | External client's SN instance |
| `servicenow-internal` | ServiceNow — Internal | Internal IT service desk |
| `jira-eng` | Jira — Engineering | Engineering team's Jira project |
| `jira-ops` | Jira — Operations | Ops team's Jira instance |

---

## ITSM Source Definition Structure

Each ITSM source definition contains three parts:

### 1. Identity

| Field | Description |
|-------|-------------|
| **Name** | A unique slug identifier (e.g. `servicenow-client-a`). Used internally in snapshot records and field mapping lookups. Immutable once data has been ingested against it. |
| **Display Label** | A human-readable label shown in the UI (e.g. `"ServiceNow — Client A"`). Can be updated at any time. |

The `name` value is stored in `raw_snapshot.itsm_source` for every snapshot uploaded against this source. It is also the foreign key referenced in `itsm_field_mapping.itsm_source`.

### 2. Field Mappings

A set of rows in the `itsm_field_mapping` table. Each row says:

> "In this source, the CSV column named `source_field_name` maps to canonical name `canonical_field_name`."

| `itsm_source` | `source_field_name` | `canonical_field_name` |
|---------------|---------------------|------------------------|
| `servicenow-client-a` | `number` | `Number` |
| `servicenow-client-a` | `company` | `Company` |
| `servicenow-client-a` | `short_description` | `Short Description` |
| `servicenow-client-a` | `state` | `State` |
| `servicenow-client-a` | `priority` | `Priority` |

Any CSV column for this source that has a mapping row is stored in `field_change` under the canonical name. Any column without a mapping row is stored under its raw source column name (fallback — no data is lost).

### 3. Required Fields

Each mapping row has an `is_required` flag (boolean, default `false`). When `is_required = true` for a mapping row, that `source_field_name` **must be present as a column header** in every CSV uploaded for this ITSM source.

If any required column is absent from the CSV header row, the upload is rejected before any data is written. The error message lists all missing required columns by name.

---

## Built-in Canonical Names

The following canonical names are the standard set used throughout UPMS. Current defaults use title-case registry names such as `Number`, `Company`, `Short Description`, and `State`. Legacy aliases such as `ticket_key`, `company`, `title`, and `status` are still accepted during import. You are not required to use all of them, and you may also use any custom canonical name by simply typing it into the mapping form.

| Canonical Name | Typical Meaning |
|----------------|-----------------|
| `Number` | The source-system ticket number (e.g. `INC0001234`, `JIRA-5678`) used to derive the stored `ticket_key` |
| `Company` | The company or customer the ticket belongs to — **must be mapped** for company scoping to work |
| `Short Description` | Short summary / subject line of the ticket |
| `Description` | Full description or body of the ticket |
| `Priority` | Priority level (e.g. `High`, `Medium`, `Low`) |
| `State` | Current workflow state (e.g. `New`, `In Progress`, `Resolved`) |
| `Created On` | When the ticket was originally created in the source system |
| `Updated On` | When the ticket was last updated in the source system |
| `Assigned To` | The person or team the ticket is assigned to |
| `Category` | The service category or issue type |

> **Important**: The `Company` canonical name is special. The ingest pipeline reads the company for each ticket from the column mapped to `Company` in the selected ITSM source. If no column is mapped to `Company`, the ingest pipeline cannot determine which company each ticket belongs to, and upload will fail.

> **Important**: The `Number` canonical name identifies the source-system ticket number. The column mapped to `Number` provides the value used when deriving `snapshot_ticket.ticket_key` for point-in-time reconstruction. If no column is mapped to `Number`, the ingest pipeline cannot create ticket records.

---

## Managing ITSM Sources

ITSM sources are managed through the **ITSM Source Management** page at `/itsm-sources` in the UPMS frontend.

### Adding a New ITSM Source

1. Navigate to **ITSM Sources** in the navigation menu.
2. Click **Add New Source**.
3. Enter:
   - **Name**: A slug identifier (lowercase, hyphens allowed, no spaces). Example: `servicenow-client-a`. This value cannot be changed after data has been ingested against it.
   - **Display Label**: A readable label for the UI. Example: `ServiceNow — Client A`.
4. Save. The new source appears in the list and is immediately available in the upload form's ITSM source dropdown.

### Adding Field Mappings

After creating a source, define field mappings to tell UPMS how your ITSM export's column names relate to canonical names.

1. From the ITSM Sources list, click the source name to open it.
2. Click **Add Mapping**.
3. Enter:
   - **Source Column Name**: The exact column header as it appears in your ITSM export CSV (e.g. `short_description`). Case-sensitive.
   - **Canonical Name**: The canonical name to use when storing this field (e.g. `Short Description`). See the built-in canonical names table above.
   - **Required**: Check this box if this column must always be present in uploaded CSV files for this source.
4. Save.

Repeat for each column you want to map. You do not need to map every column — unmapped columns are stored under their raw column name with no data loss.

### Editing a Field Mapping

1. Open the ITSM source.
2. Locate the mapping row and click **Edit**.
3. Update the canonical name or the required flag as needed.
4. Save.

> **Note**: Changing the canonical name of a mapping only affects future uploads. Existing `field_change` records already in the database are stored under the canonical name that was in effect at ingest time.

### Marking a Field as Required

1. Open the ITSM source.
2. Locate the mapping row for the field.
3. Toggle **Required** on.
4. Save.

From this point forward, any CSV uploaded for this ITSM source that does not include this column in its header row will be rejected before any data is written.

### Deleting a Field Mapping

1. Open the ITSM source.
2. Locate the mapping row and click **Delete**.
3. Confirm.

Deleting a mapping only affects future uploads. Existing data is unaffected.

### Deleting an ITSM Source

Deleting an ITSM source removes the source record and all its mapping rows. It does **not** delete any snapshots or field change records already ingested against this source — those remain in the database. The source name will no longer appear in the upload dropdown.

> **Caution**: If you delete an ITSM source that has existing snapshots, those snapshots will still reference the old source name in `raw_snapshot.itsm_source`. That data remains queryable, but there will be no mapping table to normalise field names for retrospective analysis.

---

## How Field Mapping Works During Ingest

When a CSV is uploaded:

```
User selects ITSM source: "servicenow-client-a"
User selects snapshot date: 2024-01-15
User uploads CSV file

Step 1 — Validate header row
  CSV headers: [number, company, short_description, priority, state, u_custom_99]

  Required fields for "servicenow-client-a": [number, company]
  All required fields present? ✓ Proceed.

  (If "number" were missing → reject with error: "Missing required fields: number")

Step 2 — Create snapshot record
  raw_snapshot.itsm_source = "servicenow-client-a"
  raw_snapshot.snapshot_date = 2024-01-15

Step 3 — For each data row:

  Row: number=INC0001234, company=Acme Corp, short_description=Cannot login,
       priority=High, state=In Progress, u_custom_99=BATCH-7

  Lookup canonical names via IItsmFieldMappingService.GetCanonicalName:
    "number"            → "Number"            (mapped)
    "company"           → "Company"           (mapped)
    "short_description" → "Short Description" (mapped)
    "priority"          → "Priority"          (mapped)
    "state"             → "State"             (mapped)
    "u_custom_99"       → "u_custom_99"        (NOT mapped — fallback to raw name)

  Company = row["company"] = "Acme Corp"   ← captured through the `Company` mapping
  TicketKey = TicketKeyFactory.Compose("servicenow-client-a", "Acme Corp", "INC0001234")

  Insert snapshot_ticket: (snapshotId, "servicenow-client-a::Acme Corp::INC0001234", "Acme Corp")

  Insert field_change rows:
    (ticket_key="servicenow-client-a::Acme Corp::INC0001234", company="Acme Corp", field_name="Number",            field_value="INC0001234")
    (ticket_key="servicenow-client-a::Acme Corp::INC0001234", company="Acme Corp", field_name="Company",           field_value="Acme Corp")
    (ticket_key="servicenow-client-a::Acme Corp::INC0001234", company="Acme Corp", field_name="Short Description", field_value="Cannot login")
    (ticket_key="servicenow-client-a::Acme Corp::INC0001234", company="Acme Corp", field_name="Priority",          field_value="High")
    (ticket_key="servicenow-client-a::Acme Corp::INC0001234", company="Acme Corp", field_name="State",             field_value="In Progress")
    (ticket_key="servicenow-client-a::Acme Corp::INC0001234", company="Acme Corp", field_name="u_custom_99",       field_value="BATCH-7")
```

The result: all mapped columns are normalised to canonical names; the unmapped column `u_custom_99` is preserved as-is. Nothing is discarded.

---

## Service Interface Reference

The [`IItsmFieldMappingService`](../src/UPMS.Data/IItsmFieldMappingService.cs) interface in `UPMS.Data` exposes:

```csharp
public interface IItsmFieldMappingService
{
    // Returns the canonical name for a given source and source column name.
    // Returns sourceFieldName unchanged if no mapping is found (graceful fallback).
    string GetCanonicalName(string itsmSource, string sourceFieldName);

    // Returns all mapping rows for a given ITSM source, ordered by source_field_name.
    IEnumerable<ItsmFieldMapping> GetMappingsForSource(string itsmSource);

    // Adds or updates a single field mapping row.
    Task UpsertMappingAsync(string itsmSource, string sourceFieldName, string canonicalFieldName);
}
```

The [`ItsmFieldMapping`](../src/UPMS.Data/ItsmFieldMapping.cs) data type:

```csharp
public class ItsmFieldMapping
{
    public string ItsmSource { get; set; } = string.Empty;
    public string SourceFieldName { get; set; } = string.Empty;
    public string CanonicalFieldName { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
}
```

The concrete implementation [`ItsmFieldMappingService`](../src/UPMS.Data/ItsmFieldMappingService.cs) is database-backed (EF Core + Npgsql) and is registered with DI by the API and worker when calling `AddUpmsData(...)`.

---

## Database Tables

### `itsm_source`

| Column | Type | Notes |
|--------|------|-------|
| `id` | UUID PK | Unique identifier |
| `name` | VARCHAR UNIQUE NOT NULL | Slug — used in `raw_snapshot.itsm_source` and `itsm_field_mapping.itsm_source` |
| `display_label` | VARCHAR NOT NULL | Human-readable label |
| `created_at` | TIMESTAMPTZ | Row creation time |

### `itsm_field_mapping`

| Column | Type | Notes |
|--------|------|-------|
| `itsm_source` | VARCHAR FK → `itsm_source.name` | Parent source |
| `source_field_name` | VARCHAR NOT NULL | CSV column name as exported |
| `canonical_field_name` | VARCHAR NOT NULL | Normalised name used in UPMS |
| `is_required` | BOOLEAN NOT NULL DEFAULT false | Must be present in CSV header |
| `created_at` | TIMESTAMPTZ | Row creation time |
| `updated_at` | TIMESTAMPTZ | Last updated time |

**Primary key**: `(itsm_source, source_field_name)`

**Index**: `idx_itsm_field_mapping_source` on `(itsm_source)` — supports bulk lookup of all mappings for a source during ingest.

---

## Cross-Source Normalisation

The primary benefit of canonical names is that ticket data from different ITSM sources can be queried with the same field name. Once mapped, all sources store their current state under `State`, their short summary under `Short Description`, and their assignee under `Assigned To` — regardless of what the source called those fields.

Example: Two sources, one query.

| Source | Source Column | Canonical Name |
|--------|---------------|----------------|
| `servicenow-client-a` | `state` | `State` |
| `servicenow-internal` | `incident_state` | `State` |
| `jira-eng` | `status` | `State` |

After ingest, all three sources have `field_change` rows with `field_name = "State"`. A report or query filtering on `field_name = "State"` returns consistent results across all sources without needing to know which source each ticket came from.

Sources that have not yet been mapped still work — their raw column names are stored as-is. The fallback ensures no data is ever discarded during ingest, even before mappings are configured.

---

## Related Documentation

- [README](../README.md) — top-level architecture and runtime overview
- [Docker Setup](DOCKER.md) — container runtime and local execution notes
- [UPMS Data Consumer Guide](UPMS_DATA_CONSUMER_GUIDE.md) — how to use `IItsmFieldMappingService` and `TicketDataService` together during ingest
- [Modernisation Architecture Blueprint](Modernisation/10-Architecture-Blueprint.md) — current split-runtime architecture
