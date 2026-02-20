# UPMS.Data Consumer Guide

## Overview

**UPMS.Data** is the data access layer for the Unified Problem Management System. It manages ticket data and field changes ingested from ITSM (IT Service Management) sources, tracking field values over time and allowing historical reconstruction of ticket state at any specific point in time.

---

## Architecture

### Core Components

| Component | Purpose |
|-----------|---------|
| [`TicketDataService`](../src/UPMS.Data/TicketDataService.cs) | Static service providing all snapshot, ticket, and field change I/O operations |
| [`ItsmFieldMappingService`](../src/UPMS.Data/ItsmFieldMappingService.cs) | Database-backed service for canonical field name lookups and mapping management |
| [`Ticket`](../src/UPMS.Data/Ticket.cs) | Record representing a ticket with all its fields at a point in time |
| [`FieldChange`](../src/UPMS.Data/FieldChange.cs) | Record representing a single field value observation for a ticket |
| [`ItsmFieldMapping`](../src/UPMS.Data/ItsmFieldMapping.cs) | Record representing a single source-field-name → canonical-name mapping row |
| [`DatabaseOptions`](../src/UPMS.Data/DatabaseOptions.cs) | Configuration class for database connection settings |

### Database Backend

- **Storage**: PostgreSQL (primary) with fallback support for SQLite (test environments)
- **ORM**: Dapper (lightweight, performance-focused)
- **Connection**: Npgsql driver for PostgreSQL

---

## Getting Started: Initialization

### Step 1: Add UPMS.Data to Your Project

Reference the `UPMS.Data` project in your project dependencies.

### Step 2: Initialize TicketDataService

You **must** initialize `TicketDataService` before using any operations. There are three initialization patterns:

#### Option A: From Configuration (Recommended)

```csharp
// In your Program.cs
using UPMS.Data;
using Microsoft.Extensions.Options;

services.Configure<DatabaseOptions>(config.GetSection("Database"));

// During app initialization
var options = serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>();
TicketDataService.Initialize(options);
```

**appsettings.json**:
```json
{
  "Database": {
    "ConnectionString": "Server=localhost;Database=upms;User Id=postgres;Password=password"
  }
}
```

#### Option B: From Connection String

```csharp
var connectionString = "Server=localhost;Database=upms;User Id=postgres;Password=password";
TicketDataService.Initialize(connectionString);
```

#### Option C: From Custom Connection Factory

```csharp
TicketDataService.Initialize(() => new NpgsqlConnection(connectionString));
```

### Step 3: Register ItsmFieldMappingService

`ItsmFieldMappingService` is instantiated separately and registered in DI:

```csharp
services.AddSingleton<IItsmFieldMappingService>(sp =>
    ItsmFieldMappingService.CreateFromOptions(
        sp.GetRequiredService<IOptions<DatabaseOptions>>()));
```

### Configuration Section Name

The standard configuration section name is `"Database"` (accessible via `DatabaseOptions.SectionName`).

---

## Snapshot CSV Format

UPMS ingests ticket data from flat-table CSV files exported from ITSM systems.

### Format Rules

- **Row 1 is the header row** — these are the exact column names from the ITSM export (e.g. `number`, `company`, `short_description`, `priority`, `state`).
- **Each subsequent row is one ticket** in its current state at the snapshot point in time.
- **Company is read from the data** — there is no separate company field on the upload form. The column whose name maps to canonical name `company` for the selected ITSM source provides the company name for each ticket row.
- **One CSV can contain tickets for multiple companies** as long as each row has a populated company column.

### Sample CSV

```csv
number,company,short_description,priority,state
INC0001234,Acme Corp,Cannot login to VPN,High,In Progress
INC0001235,Acme Corp,Printer not responding,Low,New
INC0001236,Globex Ltd,Email not syncing,Medium,Resolved
CHG0005678,Globex Ltd,Deploy Q4 patch bundle,High,In Progress
```

In this example, for an ITSM source named `servicenow-client-a` with the following mappings:

| Source Column | Canonical Name |
|---------------|----------------|
| `number` | `ticket_key` |
| `company` | `company` |
| `short_description` | `title` |
| `priority` | `priority` |
| `state` | `status` |

All five columns are mapped. Each row produces one `snapshot_ticket` record and five `field_change` records (one per column), stored under the canonical field names. The company for each ticket (`Acme Corp` or `Globex Ltd`) is extracted from the `company` column of each row.

### Required Fields Validation

If the ITSM source definition marks certain source column names as **required**, the ingest pipeline validates the CSV header row before writing any data. If a required column is absent from the header, the upload is rejected immediately with an error listing the missing column names.

For example, if `number` and `company` are marked as required for `servicenow-client-a`, uploading a CSV whose header row does not contain `number` or `company` will produce:

```
Validation failed. The following required fields are missing from the CSV header:
  - number
  - company
```

No records are written when validation fails.

---

## Data Input Operations

### 1. Create a Snapshot

Creates a logical container for a batch of tickets from an ITSM source at a specific point in time.

```csharp
public static async Task<Guid> CreateSnapshotAsync(
    string itsmSource,
    DateTime snapshotDate,
    string uploadedBy = "system",
    string? uploadMetadata = null)
```

**Parameters**:
- `itsmSource` (required): The ITSM source name (e.g. `"servicenow-client-a"`) — must match a name defined in the `itsm_source` table
- `snapshotDate` (required): The date/time the snapshot represents
- `uploadedBy` (optional): User or system that created the snapshot (default: `"system"`)
- `uploadMetadata` (optional): JSON or arbitrary metadata about the upload

**Returns**: A `Guid` identifier for the snapshot

**Example**:
```csharp
var snapshotId = await TicketDataService.CreateSnapshotAsync(
    itsmSource: "servicenow-client-a",
    snapshotDate: new DateTime(2024, 1, 15, 10, 30, 0),
    uploadedBy: "web-upload",
    uploadMetadata: "{\"filename\": \"snow_export_jan15.csv\", \"row_count\": 142}"
);
```

---

### 2. Add Tickets to a Snapshot

Associates ticket keys with a snapshot, scoped to a company. Each ticket must have a company name (read from the CSV data).

```csharp
public static async Task AddTicketsToSnapshotAsync(
    Guid snapshotId,
    IEnumerable<(string TicketKey, string CompanyName)> ticketsWithCompanies)
```

**Parameters**:
- `snapshotId` (required): The snapshot ID from `CreateSnapshotAsync`
- `ticketsWithCompanies` (required): Tuples of ticket key and company name, derived from the CSV data

**Behaviour**:
- Automatically validates and deduplicates tickets
- Ignores empty/null values
- Uses transactions for data consistency

**Example**:
```csharp
// Company names come from the CSV data, not from the upload form
var tickets = new[]
{
    ("INC0001234", "Acme Corp"),
    ("INC0001235", "Acme Corp"),
    ("INC0001236", "Globex Ltd"),
};

await TicketDataService.AddTicketsToSnapshotAsync(snapshotId, tickets);
```

---

### 3. Record Field Changes

Records a field value observation for a ticket. Called once per column per row during CSV ingest.

```csharp
public static async Task RecordFieldChangeAsync(
    string companyName,
    string ticketKey,
    string fieldName,
    string? fieldValue,
    DateTime observedAt,
    Guid snapshotId)
```

**Parameters**:
- `companyName` (required): Company that owns the ticket (read from the CSV row)
- `ticketKey` (required): Unique ticket identifier
- `fieldName` (required): The field name to store — either the canonical name (if the column has a mapping) or the raw source column name (if unmapped)
- `fieldValue` (optional): The field's value (can be null)
- `observedAt` (required): The snapshot date
- `snapshotId` (required): The snapshot this observation belongs to

**Behaviour**:
- Each call records one field value observation
- Mapped columns are stored under their canonical name; unmapped columns are stored under their raw source column name
- All observations for a snapshot share the same `observedAt` (the snapshot date)

**Example**:
```csharp
// fieldName here is the canonical name resolved by IItsmFieldMappingService
await TicketDataService.RecordFieldChangeAsync(
    companyName: "Acme Corp",      // read from the "company" column in the CSV row
    ticketKey: "INC0001234",       // read from the "number" column (mapped to canonical "ticket_key")
    fieldName: "status",           // canonical name for source column "state"
    fieldValue: "In Progress",
    observedAt: new DateTime(2024, 1, 15, 0, 0, 0),
    snapshotId: snapshotId
);

await TicketDataService.RecordFieldChangeAsync(
    companyName: "Acme Corp",
    ticketKey: "INC0001234",
    fieldName: "priority",         // same canonical name as source column name
    fieldValue: "High",
    observedAt: new DateTime(2024, 1, 15, 0, 0, 0),
    snapshotId: snapshotId
);
```

---

## Canonical Field Name Lookups

During ingest, each source column name is resolved to a canonical name (or kept as-is if no mapping exists) using `IItsmFieldMappingService`.

### GetCanonicalName

```csharp
string GetCanonicalName(string itsmSource, string sourceFieldName);
```

Returns the canonical name for the given ITSM source and source column name. If no mapping is defined for this combination, returns `sourceFieldName` unchanged (graceful fallback).

**Example**:
```csharp
// For ITSM source "servicenow-client-a" with mapping: short_description → title
string canonical = mappingService.GetCanonicalName("servicenow-client-a", "short_description");
// Returns: "title"

string unmapped = mappingService.GetCanonicalName("servicenow-client-a", "u_custom_field_99");
// Returns: "u_custom_field_99" (fallback — no mapping defined)
```

### GetMappingsForSource

```csharp
IEnumerable<ItsmFieldMapping> GetMappingsForSource(string itsmSource);
```

Returns all [`ItsmFieldMapping`](../src/UPMS.Data/ItsmFieldMapping.cs) rows for a given ITSM source, ordered by source field name.

```csharp
public class ItsmFieldMapping
{
    public required string ItsmSource { get; init; }
    public required string SourceFieldName { get; init; }
    public required string CanonicalFieldName { get; init; }
}
```

### UpsertMappingAsync

```csharp
Task UpsertMappingAsync(string itsmSource, string sourceFieldName, string canonicalFieldName);
```

Adds or updates a single field mapping. Used by the ITSM Source Management UI.

---

## Ingest Pseudocode

The following pseudocode illustrates how `IItsmFieldMappingService` and `TicketDataService` are used together during CSV ingest:

```
// Validate required fields first
var requiredFields = GetRequiredFieldsForSource(itsmSource);
var missingFields = requiredFields.Except(csvHeaderColumns).ToList();
if (missingFields.Any())
    return IngestResult.Failure("Missing required fields: " + string.Join(", ", missingFields));

// Create the snapshot record
var snapshotId = await TicketDataService.CreateSnapshotAsync(itsmSource, snapshotDate, uploadedBy);

// Process each data row
foreach (var row in csvDataRows)
{
    // Company comes from the row, not from the upload form
    var companyColumnName = mappingService.GetCanonicalName(itsmSource, "company") // or reverse lookup
    var company = row[companyFieldName];
    var ticketKey = row[mappingService.GetCanonicalName(itsmSource, "ticket_key")];

    await TicketDataService.AddTicketsToSnapshotAsync(snapshotId, [(ticketKey, company)]);

    foreach (var (columnName, value) in row)
    {
        var fieldName = mappingService.GetCanonicalName(itsmSource, columnName);
        await TicketDataService.RecordFieldChangeAsync(company, ticketKey, fieldName, value, snapshotDate, snapshotId);
    }
}
```

---

## Data Retrieval Operations

### 1. Get Tickets as of a Date

Retrieves all tickets for a company from a specific ITSM source as they appeared at a specific point in time. Ticket state is reconstructed by finding the latest field values observed on or before that date.

```csharp
public static async Task<IEnumerable<Ticket>> GetTicketsAsync(
    string itsmSource,
    string companyName,
    DateTime asOfDate)
```

**Parameters**:
- `itsmSource` (required): The ITSM source name (e.g. `"servicenow-client-a"`)
- `companyName` (required): Filter tickets by company
- `asOfDate` (required): Point-in-time for state reconstruction

**Returns**: `IEnumerable<Ticket>` with current field state as of the date

**Example**:
```csharp
var tickets = await TicketDataService.GetTicketsAsync(
    itsmSource: "servicenow-client-a",
    companyName: "Acme Corp",
    asOfDate: new DateTime(2024, 1, 15, 10, 30, 0)
);

foreach (var ticket in tickets)
{
    Console.WriteLine($"Ticket: {ticket.TicketKey}");
    Console.WriteLine($"  Status: {ticket.Fields["status"]}");   // canonical name
    Console.WriteLine($"  Priority: {ticket.Fields["priority"]}");
}
```

**Ticket Structure**:
```csharp
public class Ticket
{
    public string TicketKey { get; init; }
    public string CompanyName { get; init; }
    public string ItsmSource { get; init; }
    public IDictionary<string, string?> Fields { get; init; }  // keyed by canonical name (or raw column name if unmapped)
    public DateTime ObservedAt { get; init; }
    public Guid SnapshotId { get; init; }
    public DateTime SnapshotDate { get; init; }
}
```

---

### 2. Get All Tickets in a Snapshot

Retrieves all tickets from a specific snapshot, reconstructed as they appeared at the snapshot date.

```csharp
public static async Task<IEnumerable<Ticket>> GetTicketsBySnapshotAsync(Guid snapshotId)
```

**Example**:
```csharp
var tickets = await TicketDataService.GetTicketsBySnapshotAsync(snapshotId);
Console.WriteLine($"Retrieved {tickets.Count()} tickets from snapshot");
```

---

### 3. Get Field Change History

Retrieves the complete observation history for a specific field of a ticket in chronological order.

```csharp
public static async Task<IEnumerable<FieldChange>> GetTicketFieldHistoryAsync(
    string companyName,
    string ticketKey,
    string fieldName)
```

**Parameters**:
- `companyName` (required): Company that owns the ticket
- `ticketKey` (required): Ticket identifier
- `fieldName` (required): The field name to get history for — use the canonical name if the field was mapped

**Returns**: `IEnumerable<FieldChange>` ordered by observation time (ascending)

**Example**:
```csharp
// Query using canonical name "status"
var statusHistory = await TicketDataService.GetTicketFieldHistoryAsync(
    companyName: "Acme Corp",
    ticketKey: "INC0001234",
    fieldName: "status"
);

foreach (var change in statusHistory)
{
    Console.WriteLine($"  {change.ObservedAt:yyyy-MM-dd} -> {change.FieldValue}");
}
```

**FieldChange Structure**:
```csharp
public class FieldChange
{
    public long Id { get; init; }
    public string CompanyName { get; init; }
    public string TicketKey { get; init; }
    public string FieldName { get; init; }       // canonical name (or raw column name if unmapped)
    public string? FieldValue { get; init; }
    public DateTime ObservedAt { get; init; }
    public Guid SnapshotId { get; init; }
}
```

---

## Database Schema

### Table: `itsm_source`

One row per defined ITSM source instance.

| Column | Type | Purpose |
|--------|------|---------|
| `id` | UUID (PK) | Unique source identifier |
| `name` | VARCHAR UNIQUE | Slug identifier used in snapshot records (e.g. `servicenow-client-a`) |
| `display_label` | VARCHAR | Human-readable name shown in the UI |
| `created_at` | TIMESTAMPTZ | Row creation timestamp |

### Table: `itsm_field_mapping`

One row per field mapping per ITSM source instance.

| Column | Type | Purpose |
|--------|------|---------|
| `itsm_source` | VARCHAR (FK → `itsm_source.name`) | The ITSM source this mapping belongs to |
| `source_field_name` | VARCHAR | Column name as it appears in the CSV export |
| `canonical_field_name` | VARCHAR | Normalised name used throughout UPMS |
| `is_required` | BOOLEAN | If true, this column must be present in every CSV for this source |
| `created_at` | TIMESTAMPTZ | Row creation timestamp |
| `updated_at` | TIMESTAMPTZ | Last update timestamp |

**Primary key**: `(itsm_source, source_field_name)`

### Table: `raw_snapshot`

Stores snapshot metadata.

| Column | Type | Purpose |
|--------|------|---------|
| `id` | UUID (PK) | Unique snapshot identifier |
| `itsm_source` | VARCHAR | ITSM source name — references `itsm_source.name` |
| `snapshot_date` | TIMESTAMPTZ | The point in time the snapshot represents |
| `uploaded_by` | VARCHAR | User or system that uploaded |
| `uploaded_at` | TIMESTAMPTZ | When the upload occurred |
| `upload_metadata` | JSONB | Optional metadata (filename, row count, etc.) |

### Table: `snapshot_ticket`

Links ticket keys to snapshots.

| Column | Type | Purpose |
|--------|------|---------|
| `id` | UUID (PK) | Unique row identifier |
| `snapshot_id` | UUID (FK) | References `raw_snapshot.id` |
| `company_name` | VARCHAR | Ticket's company — read from the CSV data |
| `ticket_key` | VARCHAR | Unique ticket identifier |

**Unique constraint**: `(snapshot_id, ticket_key)` prevents duplicate tickets in a snapshot

### Table: `field_change`

Records individual field value observations.

| Column | Type | Purpose |
|--------|------|---------|
| `id` | BIGINT (PK) | Auto-incrementing row ID |
| `company_name` | VARCHAR | Ticket's company — read from the CSV data |
| `ticket_key` | VARCHAR | Ticket identifier |
| `field_name` | VARCHAR | Canonical field name (or raw source column name if unmapped) |
| `field_value` | TEXT | The observed value (nullable) |
| `observed_at` | TIMESTAMPTZ | The snapshot date |
| `snapshot_id` | UUID (FK) | Associated snapshot |

---

## Configuration

### appsettings.json Example

```json
{
  "Database": {
    "ConnectionString": "Server=localhost;Port=5432;Database=upms_production;User Id=upms_user;Password=SecurePassword123;Application Name=UPMS"
  }
}
```

### Environment Variables

```bash
Database__ConnectionString=Server=db.example.com;Database=upms;User Id=user;Password=pass
```

---

## Example: Complete Ingest Workflow

```csharp
using UPMS.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

// 1. Initialize
var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .AddEnvironmentVariables()
    .Build();

var dbOptions = Options.Create(new DatabaseOptions
{
    ConnectionString = config.GetConnectionString("Database")!
});

TicketDataService.Initialize(dbOptions);

var mappingService = ItsmFieldMappingService.CreateFromOptions(dbOptions);

// 2. Define the ITSM source name
string itsmSource = "servicenow-client-a";

// 3. Read the CSV header and validate required fields
string[] csvHeader = ["number", "company", "short_description", "priority", "state"];
var allMappings = mappingService.GetMappingsForSource(itsmSource).ToList();
// (required field validation happens here — omitted for brevity)

// 4. Create the snapshot — no company name on the snapshot itself
var snapshotId = await TicketDataService.CreateSnapshotAsync(
    itsmSource: itsmSource,
    snapshotDate: new DateTime(2024, 1, 15, 0, 0, 0),
    uploadedBy: "web-upload"
);

// 5. Process CSV rows — company comes from each row's data
var csvRows = new[]
{
    new Dictionary<string, string> {
        ["number"] = "INC0001234", ["company"] = "Acme Corp",
        ["short_description"] = "Cannot login to VPN", ["priority"] = "High", ["state"] = "In Progress"
    },
    new Dictionary<string, string> {
        ["number"] = "INC0001236", ["company"] = "Globex Ltd",
        ["short_description"] = "Email not syncing", ["priority"] = "Medium", ["state"] = "Resolved"
    },
};

var ticketsWithCompanies = new List<(string, string)>();
var fieldChanges = new List<(string company, string key, string field, string? value)>();

foreach (var row in csvRows)
{
    // Resolve company and ticket key from mapped columns
    string companyColumn = allMappings.FirstOrDefault(m => m.CanonicalFieldName == "company")?.SourceFieldName ?? "company";
    string ticketKeyColumn = allMappings.FirstOrDefault(m => m.CanonicalFieldName == "ticket_key")?.SourceFieldName ?? "number";

    string company = row[companyColumn];
    string ticketKey = row[ticketKeyColumn];

    ticketsWithCompanies.Add((ticketKey, company));

    foreach (var (col, val) in row)
    {
        string fieldName = mappingService.GetCanonicalName(itsmSource, col);
        fieldChanges.Add((company, ticketKey, fieldName, val));
    }
}

await TicketDataService.AddTicketsToSnapshotAsync(snapshotId, ticketsWithCompanies);

foreach (var (company, key, field, value) in fieldChanges)
{
    await TicketDataService.RecordFieldChangeAsync(
        companyName: company,
        ticketKey: key,
        fieldName: field,
        fieldValue: value,
        observedAt: new DateTime(2024, 1, 15, 0, 0, 0),
        snapshotId: snapshotId
    );
}

// 6. Retrieve tickets as of a date using canonical field names
var tickets = await TicketDataService.GetTicketsAsync(
    itsmSource: itsmSource,
    companyName: "Acme Corp",
    asOfDate: new DateTime(2024, 1, 15, 0, 0, 0)
);

foreach (var ticket in tickets)
{
    Console.WriteLine($"Ticket: {ticket.TicketKey}");
    Console.WriteLine($"  Status: {ticket.Fields["status"]}");    // canonical name
    Console.WriteLine($"  Priority: {ticket.Fields["priority"]}");
    Console.WriteLine($"  Title: {ticket.Fields["title"]}");
}
```

---

## Key Design Patterns

### 1. Flat-Table CSV with Company from Data

The CSV format is a flat table: row 1 is the header (source column names), each subsequent row is one ticket. The company for each ticket is read from the column mapped to canonical name `company` — it is never a separate upload-time input. This allows a single CSV file to contain tickets for multiple companies.

### 2. Source-Scoped Field Mappings

Field mappings belong to a specific **named ITSM source instance** (`itsm_source.name`), not to a tool type. Two ServiceNow instances with different field names each have their own mapping table. The same source column name can map to different canonical names in different ITSM sources.

### 3. Canonical Names in Storage

After ingest, `field_change.field_name` contains:
- The **canonical name** for any column that had a mapping defined for the selected ITSM source
- The **raw source column name** for any unmapped column (graceful fallback)

Consumers should query using canonical names (e.g. `"status"`, `"priority"`) to get consistent results across all ITSM sources.

### 4. Required Field Validation

If `itsm_field_mapping.is_required = true` for a source column, the ingest pipeline validates the CSV header row before writing any data. A missing required column causes the entire upload to be rejected with an error message listing the missing fields.

### 5. Append-Only History

Data is never modified after ingest. Each snapshot adds new `field_change` rows. Point-in-time reconstruction selects the latest observed value for each field up to the requested timestamp.

### 6. Static Service Pattern

`TicketDataService` is a static class initialized once per application lifetime. `IItsmFieldMappingService` is an instance-based interface registered in DI.

---

## Error Handling

| Exception | Trigger |
|-----------|---------|
| `ArgumentNullException` | Connection factory or options not initialized |
| `ArgumentException` | Required parameter is null, empty, or invalid |
| `InvalidOperationException` | `TicketDataService` not initialized before use |
| `NpgsqlException` | Database connection or query failure |

**Best Practice**: Always initialize before first use, and handle `InvalidOperationException` in your calling code.

---

## Performance Considerations

1. **Batch Operations**: `AddTicketsToSnapshotAsync` uses transactions for efficiency
2. **Window Functions**: Reconstruction queries use SQL window functions for set-based performance
3. **Indexing**: Indexes exist on `company_name`, `ticket_key`, `itsm_source`, `observed_at`, and `field_name`
4. **Connection Pooling**: Always use connection pooling in production

---

## Summary

| Aspect | Details |
|--------|---------|
| **Storage** | PostgreSQL (Npgsql) + SQLite fallback for tests |
| **ORM** | Dapper |
| **Main Service** | `TicketDataService` (static) |
| **Mapping Service** | `IItsmFieldMappingService` / `ItsmFieldMappingService` (instance, DI) |
| **CSV Format** | Flat table — row 1 = header, each row = one ticket, company from data |
| **Data Input** | Snapshots, Tickets (company from CSV), Field Changes (canonical names) |
| **Data Retrieval** | Point-in-time queries, snapshot queries, field history queries |
| **Configuration** | `DatabaseOptions` from appsettings.json |
| **Core Pattern** | Append-only field change history with canonical name normalisation per ITSM source |
