# UPMS.Data Consumer Guide

## Overview

**UPMS.Data** is a data access layer that manages ticket data and field changes for ITSM (IT Service Management) sources. It tracks tickets and their field changes over time, allowing historical reconstruction of ticket state at specific points in time.

---

## Architecture

### Core Components

| Component | Purpose |
|-----------|---------|
| **TicketDataService** | Static service providing all data I/O operations |
| **Ticket** | Record representing a ticket with all its fields at a point in time |
| **FieldChange** | Record representing a single field value change for a ticket |
| **DatabaseOptions** | Configuration class for database connection settings |

### Database Backend

- **Storage**: PostgreSQL (primary) with fallback support for SQLite
- **ORM**: Dapper (lightweight, performance-focused)
- **Connection**: Npgsql driver for PostgreSQL

---

## Getting Started: Initialization

### Step 1: Add UPMS.Data to Your Project

Reference the `UPMS.Data` NuGet package in your project dependencies.

### Step 2: Initialize the Service

You **must** initialize `TicketDataService` before using any operations. There are three initialization patterns:

#### Option A: From Configuration (Recommended)

```csharp
// In your Startup/Program.cs
using UPMS.Data;
using Microsoft.Extensions.Options;

// Assuming IOptions<DatabaseOptions> is registered in DI
var services = new ServiceCollection();
services.Configure<DatabaseOptions>(config.GetSection("Database"));

// Later, during app initialization
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
// Useful for dependency injection or connection pooling
TicketDataService.Initialize(() => new NpgsqlConnection(connectionString));
```

### Configuration Section Name

The standard configuration section name is `"Database"` (accessible via `DatabaseOptions.SectionName`).

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
- `itsmSource` (required): Name/identifier of the ITSM system (e.g., "Jira", "ServiceNow")
- `snapshotDate` (required): The date/time the snapshot represents
- `uploadedBy` (optional): User/system that created the snapshot (default: "system")
- `uploadMetadata` (optional): JSON or arbitrary metadata about the upload

**Returns**: A `Guid` identifier for the snapshot

**Example**:
```csharp
var snapshotId = await TicketDataService.CreateSnapshotAsync(
    itsmSource: "ServiceNow",
    snapshotDate: new DateTime(2024, 1, 15, 10, 30, 0),
    uploadedBy: "integration-service",
    uploadMetadata: "{\"source\": \"api\", \"version\": \"1.0\"}"
);
```

---

### 2. Add Tickets to a Snapshot

Associates tickets with a snapshot. Each ticket must have a company name.

```csharp
public static async Task AddTicketsToSnapshotAsync(
    Guid snapshotId,
    IEnumerable<(string TicketKey, string CompanyName)> ticketsWithCompanies)
```

**Parameters**:
- `snapshotId` (required): The snapshot ID from `CreateSnapshotAsync`
- `ticketsWithCompanies` (required): Enumerable of tuples containing ticket identifiers and company names

**Behavior**:
- Automatically validates and deduplicates tickets
- Ignores empty/null values
- Uses transactions for data consistency

**Example**:
```csharp
var tickets = new[]
{
    ("TICK-001", "Acme Corp"),
    ("TICK-002", "Acme Corp"),
    ("TICK-003", "Global Industries"),
};

await TicketDataService.AddTicketsToSnapshotAsync(snapshotId, tickets);
```

---

### 3. Record Field Changes

Records when a ticket field changes value. Called for each field change observation.

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
- `companyName` (required): Company that owns the ticket
- `ticketKey` (required): Unique ticket identifier
- `fieldName` (required): Name of the field being changed (e.g., "Status", "Priority", "Assignee")
- `fieldValue` (optional): New value of the field (can be null)
- `observedAt` (required): When the change was observed
- `snapshotId` (required): The snapshot this change is part of

**Behavior**:
- Each call records one field change observation
- Stores the history of all changes
- Allows reconstruction of ticket state at any point in time

**Example**:
```csharp
await TicketDataService.RecordFieldChangeAsync(
    companyName: "Acme Corp",
    ticketKey: "TICK-001",
    fieldName: "Status",
    fieldValue: "In Progress",
    observedAt: new DateTime(2024, 1, 15, 10, 30, 0),
    snapshotId: snapshotId
);

await TicketDataService.RecordFieldChangeAsync(
    companyName: "Acme Corp",
    ticketKey: "TICK-001",
    fieldName: "Assignee",
    fieldValue: "john.doe@acme.com",
    observedAt: new DateTime(2024, 1, 15, 10, 30, 0),
    snapshotId: snapshotId
);
```

---

## Data Retrieval Operations

### 1. Get Tickets as of a Date

Retrieves all tickets for a company from a specific ITSM source as they appeared at a specific point in time. The ticket state is reconstructed by finding the latest field values observed on or before that date.

```csharp
public static async Task<IEnumerable<Ticket>> GetTicketsAsync(
    string itsmSource,
    string companyName,
    DateTime asOfDate)
```

**Parameters**:
- `itsmSource` (required): The ITSM source identifier
- `companyName` (required): Filter tickets by company
- `asOfDate` (required): Point-in-time for state reconstruction

**Returns**: `IEnumerable<Ticket>` with current field state as of the date

**Example**:
```csharp
var tickets = await TicketDataService.GetTicketsAsync(
    itsmSource: "ServiceNow",
    companyName: "Acme Corp",
    asOfDate: new DateTime(2024, 1, 15, 10, 30, 0)
);

foreach (var ticket in tickets)
{
    Console.WriteLine($"Ticket: {ticket.TicketKey}");
    Console.WriteLine($"  Status: {ticket.Fields["Status"]}");
    Console.WriteLine($"  Priority: {ticket.Fields["Priority"]}");
    Console.WriteLine($"  Observed at: {ticket.ObservedAt}");
}
```

**Ticket Structure**:
```csharp
public class Ticket
{
    public string TicketKey { get; init; }           // "TICK-001"
    public string CompanyName { get; init; }         // "Acme Corp"
    public string ItsmSource { get; init; }          // "ServiceNow"
    public IDictionary<string, string?> Fields { get; init; }  // Field name ? value
    public DateTime ObservedAt { get; init; }        // Latest field change time
    public Guid SnapshotId { get; init; }            // Associated snapshot
    public DateTime SnapshotDate { get; init; }      // Snapshot creation date
}
```

---

### 2. Get All Tickets in a Snapshot

Retrieves all tickets from a specific snapshot, reconstructed as they appeared at the snapshot date.

```csharp
public static async Task<IEnumerable<Ticket>> GetTicketsBySnapshotAsync(Guid snapshotId)
```

**Parameters**:
- `snapshotId` (required): The snapshot to query

**Returns**: `IEnumerable<Ticket>` with all tickets in that snapshot

**Example**:
```csharp
var allTickets = await TicketDataService.GetTicketsBySnapshotAsync(snapshotId);
Console.WriteLine($"Retrieved {allTickets.Count()} tickets from snapshot");
```

---

### 3. Get Field Change History

Retrieves the complete change history for a specific field of a ticket in chronological order.

```csharp
public static async Task<IEnumerable<FieldChange>> GetTicketFieldHistoryAsync(
    string companyName,
    string ticketKey,
    string fieldName)
```

**Parameters**:
- `companyName` (required): Company that owns the ticket
- `ticketKey` (required): Ticket identifier
- `fieldName` (required): Name of the field to get history for

**Returns**: `IEnumerable<FieldChange>` ordered by observation time (ascending)

**Example**:
```csharp
var statusHistory = await TicketDataService.GetTicketFieldHistoryAsync(
    companyName: "Acme Corp",
    ticketKey: "TICK-001",
    fieldName: "Status"
);

foreach (var change in statusHistory)
{
    Console.WriteLine($"  {change.ObservedAt:yyyy-MM-dd HH:mm:ss} ? {change.FieldValue}");
}
```

**FieldChange Structure**:
```csharp
public class FieldChange
{
    public long Id { get; init; }                  // Database row ID
    public string CompanyName { get; init; }       // "Acme Corp"
    public string TicketKey { get; init; }         // "TICK-001"
    public string FieldName { get; init; }         // "Status"
    public string? FieldValue { get; init; }       // New value (can be null)
    public DateTime ObservedAt { get; init; }      // When change occurred
    public Guid SnapshotId { get; init; }          // Associated snapshot
}
```

---

## Data Storage

### Database Schema

The system uses three main tables:

#### `raw_snapshot`
Stores snapshot metadata.

| Column | Type | Purpose |
|--------|------|---------|
| `id` | UUID (PK) | Unique snapshot identifier |
| `itsm_source` | VARCHAR | ITSM system name (e.g., "ServiceNow") |
| `snapshot_date` | TIMESTAMP | When the snapshot represents |
| `uploaded_by` | VARCHAR | User/system that uploaded |
| `uploaded_at` | TIMESTAMP | When upload occurred |
| `upload_metadata` | TEXT | Optional JSON metadata |

#### `snapshot_ticket`
Links tickets to snapshots (many-to-many).

| Column | Type | Purpose |
|--------|------|---------|
| `id` | UUID (PK) | Unique row identifier |
| `snapshot_id` | UUID (FK) | References `raw_snapshot.id` |
| `company_name` | VARCHAR | Ticket's company |
| `ticket_key` | VARCHAR | Unique ticket identifier |

**Unique Constraint**: `(snapshot_id, ticket_key)` prevents duplicate tickets in a snapshot

#### `field_change`
Records individual field value changes.

| Column | Type | Purpose |
|--------|------|---------|
| `id` | BIGINT (PK) | Auto-incrementing row ID |
| `company_name` | VARCHAR | Ticket's company |
| `ticket_key` | VARCHAR | Ticket identifier |
| `field_name` | VARCHAR | Name of the field (e.g., "Status") |
| `field_value` | TEXT | The value (nullable) |
| `observed_at` | TIMESTAMP | When change was observed |
| `snapshot_id` | UUID (FK) | Associated snapshot |

### Data Flow

```
TicketDataService Operations
    ?
 Dapper ORM
    ?
Npgsql Driver
    ?
PostgreSQL Database
    ?
?????????????????????????????????????????????????????????????
?  raw_snapshot       ?  snapshot_ticket ?  field_change    ?
?????????????????????????????????????????????????????????????
? Snapshot metadata   ? Ticket roster    ? Historical data  ?
? (when, who, source) ? (which tickets)  ? (what changed)   ?
?????????????????????????????????????????????????????????????
```

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

Alternatively, override via environment variable:
```bash
Database__ConnectionString=Server=db.example.com;Database=upms;User Id=user;Password=pass
```

### Connection String Format (PostgreSQL)

```
Server=hostname;Port=5432;Database=database_name;User Id=username;Password=password;[options]
```

**Common Options**:
- `Timeout=30` - Connection timeout in seconds
- `Application Name=AppName` - Identify your app in logs
- `Pooling=true` - Use connection pooling (recommended)

---

## Example: Complete Workflow

```csharp
using UPMS.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

// 1. Load configuration
var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .AddEnvironmentVariables()
    .Build();

var options = Options.Create(new DatabaseOptions 
{ 
    ConnectionString = config.GetConnectionString("Database")!
});

// 2. Initialize the service
TicketDataService.Initialize(options);

// 3. Create a snapshot
var snapshotId = await TicketDataService.CreateSnapshotAsync(
    itsmSource: "ServiceNow",
    snapshotDate: DateTime.UtcNow,
    uploadedBy: "batch-importer"
);

// 4. Add tickets to snapshot
await TicketDataService.AddTicketsToSnapshotAsync(
    snapshotId,
    new[]
    {
        ("INC0001234", "Acme Corp"),
        ("INC0001235", "Acme Corp"),
        ("CHG0005678", "Global Industries"),
    }
);

// 5. Record field changes
await TicketDataService.RecordFieldChangeAsync(
    companyName: "Acme Corp",
    ticketKey: "INC0001234",
    fieldName: "Status",
    fieldValue: "New",
    observedAt: DateTime.UtcNow,
    snapshotId: snapshotId
);

await TicketDataService.RecordFieldChangeAsync(
    companyName: "Acme Corp",
    ticketKey: "INC0001234",
    fieldName: "Priority",
    fieldValue: "High",
    observedAt: DateTime.UtcNow,
    snapshotId: snapshotId
);

// 6. Retrieve tickets as of a date
var tickets = await TicketDataService.GetTicketsAsync(
    itsmSource: "ServiceNow",
    companyName: "Acme Corp",
    asOfDate: DateTime.UtcNow
);

foreach (var ticket in tickets)
{
    Console.WriteLine($"Ticket: {ticket.TicketKey}");
    Console.WriteLine($"  Status: {ticket.Fields["Status"]}");
    Console.WriteLine($"  Priority: {ticket.Fields["Priority"]}");
}

// 7. Get field history for auditing
var statusHistory = await TicketDataService.GetTicketFieldHistoryAsync(
    companyName: "Acme Corp",
    ticketKey: "INC0001234",
    fieldName: "Status"
);

Console.WriteLine("Status change history:");
foreach (var change in statusHistory)
{
    Console.WriteLine($"  {change.ObservedAt:yyyy-MM-dd HH:mm:ss} ? {change.FieldValue}");
}
```

---

## Key Design Patterns

### 1. Snapshot-Based History Tracking

Rather than storing just current values, the system:
- Creates snapshots at specific points in time
- Records field changes within each snapshot
- Allows reconstruction of state at any point in time

**Benefit**: Complete audit trail and point-in-time queries.

### 2. Dapper + Raw SQL

- Uses Dapper for performance and control
- Raw SQL queries for complex joins and window functions
- Supports multiple database backends (PostgreSQL, SQLite)

### 3. Static Service Pattern

- `TicketDataService` is a static class
- Initialization happens once per application lifetime
- No instance creation needed (simpler API)

### 4. Immutable Records

- `Ticket` and `FieldChange` use C# `record` types
- `init`-only properties prevent accidental mutation
- Ensures data consistency and thread safety

---

## Error Handling

All public methods validate inputs and throw appropriate exceptions:

| Exception | Trigger |
|-----------|---------|
| `ArgumentNullException` | Connection factory or options not initialized |
| `ArgumentException` | Required parameter is null/empty/invalid |
| `InvalidOperationException` | Service not initialized before use |
| `NpgsqlException` | Database connection/query failure |

**Best Practice**: Always initialize before first use, and handle `InvalidOperationException` in your calling code.

---

## Performance Considerations

1. **Batch Operations**: The `AddTicketsToSnapshotAsync` method uses transactions for efficiency
2. **Window Functions**: Complex queries use SQL window functions for performance
3. **Indexing**: Ensure indexes exist on `company_name`, `ticket_key`, `itsm_source`, and `observed_at`
4. **Connection Pooling**: Always use connection pooling in production

---

## Canonical Field Mapping

> **Planned — not yet implemented** in the current version of `UPMS.Data`.

### What It Is

Different ITSM sources use different field names for semantically identical data. The canonical field mapping system provides a reference table (`itsm_field_mapping`) that maps source-specific field names to a single normalised (canonical) name used consistently throughout UPMS.

**Table: `itsm_field_mapping`**

| Column | Type | Description |
|--------|------|-------------|
| `itsm_source` | `VARCHAR` | The ITSM source system (e.g. `"ServiceNow"`, `"Jira"`) |
| `source_field_name` | `VARCHAR` | The field name as it appears in exports from that source |
| `canonical_field_name` | `VARCHAR` | The normalised field name used internally in UPMS |

### Why It Exists

Without canonical mapping, the same concept — such as ticket status — appears under different names depending on the source:

| ITSM Source | Source Field Name | Canonical Field Name |
|-------------|-------------------|----------------------|
| ServiceNow  | `incident_state`  | `Status`             |
| Jira        | `status`          | `Status`             |
| ServiceNow  | `assigned_to`     | `Assignee`           |
| Jira        | `assignee`        | `Assignee`           |
| ServiceNow  | `short_description` | `Summary`          |
| Jira        | `summary`         | `Summary`            |

Without mapping, queries and reports would need to handle source-specific field names separately, and ticket data from different sources could not be compared directly.

### How It Will Be Used

During ingest (file upload), the source field names found in the uploaded file are looked up in `itsm_field_mapping` for the relevant ITSM source. The canonical field name is used when writing `field_change` records to the database.

Pseudocode:

```
for each field in uploaded ticket:
    canonicalName = mappingService.Lookup(itsmSource, sourceFieldName)
                    ?? sourceFieldName   // fallback: use source name if no mapping found
    RecordFieldChange(ticketKey, canonicalName, fieldValue, ...)
```

Once canonical mapping is in place, all `field_change` records use canonical field names regardless of their origin. Consumers of `TicketDataService` can query using canonical names (e.g. `"Status"`) and receive consistent results across all ITSM sources.

### Planned API Addition

A future `ItsmFieldMappingService` (in `UPMS.Data`) will expose:

```csharp
// Look up the canonical name for a given source field name
string? GetCanonicalName(string itsmSource, string sourceFieldName);

// Get all mappings for an ITSM source
IEnumerable<ItsmFieldMapping> GetMappingsForSource(string itsmSource);
```

---

## Summary

| Aspect | Details |
|--------|---------|
| **Storage** | PostgreSQL (Npgsql) + SQLite fallback |
| **ORM** | Dapper |
| **Main Service** | `TicketDataService` (static) |
| **Data Input** | Snapshots, Tickets, Field Changes |
| **Data Retrieval** | Point-in-time queries, Snapshot queries, History queries |
| **Configuration** | `DatabaseOptions` from appsettings.json |
| **Core Pattern** | Snapshot-based history tracking with field change auditing |
| **Canonical Mapping** | `itsm_field_mapping` table — normalises field names across ITSM sources *(planned)* |
