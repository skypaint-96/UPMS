# UPMS.Data Consumer Guide

This guide explains how to use the `UPMS.Data` library to **store snapshots** and **reconstruct ticket state at a point in time**.

---

## Overview

`UPMS.Data` provides:

- EF Core models (`UpmsDbContext`) for UPMS tables
- `TicketDataService` for:
  - creating snapshots
  - storing “rosters” (tickets that exist in a snapshot)
  - storing an append-only field history
  - reconstructing a ticket (or list of tickets) “as-of” a chosen time
- Services for managing ITSM sources and their field mappings:
  - `IItsmSourceService`
  - `IItsmFieldMappingService`

In UPMS itself, CSV/JSON parsing and ingest orchestration happens in `UPMS.Ingestion` and is hosted by `UPMS.Api` and `UPMS.Worker`.

---

## Architecture

### Core components

- **`UpmsDbContext`**
  - EF Core DB context mapping:
    - `raw_snapshot`
    - `snapshot_ticket`
    - `field_change`
    - `itsm_source`
    - `itsm_field_mapping`

- **`TicketDataService`** (scoped)
  - Core write and point-in-time read logic

- **`TicketDataServiceInstance`** (scoped)
  - Thin wrapper used by higher-level services. External consumers can inject either `TicketDataService` or `TicketDataServiceInstance`.

- **`ItsmSourceService` / `ItsmFieldMappingService`**
  - Manage sources and mappings used by ingest

### Database backend

- Production DB: PostgreSQL (via EF Core + Npgsql)
- Tests: use SQLite in-memory (also via EF Core)

Schema is managed via EF Core migrations (`src/UPMS.Data/Migrations`). `UPMS.Api` and `UPMS.Worker` apply migrations automatically at startup.

---

## Getting Started

### Option A: Register with dependency injection (recommended)

In an ASP.NET app / worker / console app using `Host.CreateDefaultBuilder`:

```csharp
using Microsoft.Extensions.Hosting;
using UPMS.Data;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((ctx, services) =>
    {
        services.AddUpmsData(ctx.Configuration);
        services.AddScoped<TicketDataServiceInstance>();
    })
    .Build();

await host.RunAsync();
```

Connection string resolution order:

1. `UPMS_CONNECTION_STRING` environment variable
2. `ConnectionStrings:DefaultConnection`
3. `Database:ConnectionString`

### Option B: Manual construction (useful for small tools/tests)

```csharp
using Microsoft.EntityFrameworkCore;
using UPMS.Data;

var options = new DbContextOptionsBuilder<UpmsDbContext>()
    .UseNpgsql("Host=localhost;Port=5432;Database=upms;Username=upms;Password=upms")
    .Options;

using var db = new UpmsDbContext(options);
var ticketData = new TicketDataService(db);
var ticketDataInstance = new TicketDataServiceInstance(ticketData);
```

---

## Ticket Keys

UPMS uses a **single string key** per ticket (`ticket_key`).

The recommended format is:

```
<itsmSource>::<company>::<ticketNumber>
```

Use `TicketKeyFactory.Compose(...)` and `TicketKeyFactory.TryParse(...)`.

This allows ticket numbers to collide across sources/companies while keeping keys unique.

---

## Snapshot Ingest Basics

A snapshot ingest typically does:

1. Create a snapshot (`raw_snapshot`)
2. Add the roster of tickets in that snapshot (`snapshot_ticket`)
3. Record field values observed for each ticket (`field_change`)

In `UPMS.Ingestion`, `SnapshotIngestService` handles parsing CSV/JSON and uses:

- `IItsmSourceService` to validate required columns and apply source→canonical mappings
- `TicketDataService` to write snapshot data

If you are ingesting outside the hosted API and worker path, follow the same pattern.

---

## Snapshot CSV Format (used by UPMS.Ingestion)

The UPMS ingestion services expect a “flat table” CSV:

- One row = one ticket
- One column = one field

Minimum expectations:

- There must be a mapping for canonical field **`Company`**
- There must be a mapping for canonical field **`Number`** (legacy aliases such as `ticket_number` and `ticket_key` are also accepted)

The ingest service derives `ticket_key` using:

```csharp
TicketKeyFactory.Compose(itsmSourceName, companyName, ticketNumber)
```

Required fields:

- You can mark source fields as `IsRequired` in `itsm_field_mapping`.
- Upload will fail if required source columns are missing.

---

## Data Input Operations

### 1) Create a snapshot

```csharp
Guid snapshotId = await ticketData.CreateSnapshotAsync(
    itsmSource: "servicenow",
    snapshotDate: new DateTime(2026, 01, 01, 0, 0, 0, DateTimeKind.Utc),
    uploadedBy: "system");
```

### 2) Add tickets to the snapshot roster

```csharp
await ticketData.AddTicketsToSnapshotAsync(snapshotId, new[]
{
    (TicketKey: "servicenow::Acme::INC0001", CompanyName: "Acme"),
    (TicketKey: "servicenow::Acme::INC0002", CompanyName: "Acme")
});
```

### 3) Record field changes

```csharp
await ticketData.RecordFieldChangeAsync(
    companyName: "Acme",
    ticketKey: "servicenow::Acme::INC0001",
    fieldName: "status",
    fieldValue: "open",
    observedAt: new DateTime(2026, 01, 01, 0, 0, 0, DateTimeKind.Utc),
    snapshotId: snapshotId);
```

Notes:

- `field_change` is **append-only**.
- Field names are typically canonical names (e.g., `status`, `priority`) after mapping.

---

## Data Retrieval Operations

### 1) Get tickets as-of a date

```csharp
var tickets = await ticketData.GetTicketsAsync(
    itsmSource: "servicenow",
    companyName: "Acme",
    asOfDate: new DateTime(2026, 01, 15, 0, 0, 0, DateTimeKind.Utc));
```

This returns reconstructed tickets that:

- existed in at least one snapshot `<= asOfDate`
- and returns field values from the latest snapshot `<= asOfDate`

### 2) Get all tickets in a snapshot

```csharp
var tickets = await ticketData.GetTicketsBySnapshotAsync(snapshotId);
```

### 3) Get field change history for a ticket

```csharp
var history = await ticketData.GetTicketHistoryAsync(
    companyName: "Acme",
    ticketKey: "servicenow::Acme::INC0001");
```

---

## ITSM Source Mappings

The ingest layer uses source + field mapping services:

- `IItsmSourceService` manages `itsm_source` rows.
- `IItsmFieldMappingService` manages `itsm_field_mapping` rows.

Canonical name lookup:

```csharp
string? canonical = await itsmSourceService.GetCanonicalNameAsync("servicenow", "State");
```

Required fields:

```csharp
IReadOnlyList<string> required = await itsmSourceService.GetRequiredFieldsAsync("servicenow");
```

---

## Database Schema (high level)

### `itsm_source`

- `id` (int, identity)
- `name` (text, unique)
- `display_label` (text)

### `itsm_field_mapping`

- `itsm_source` (text)
- `source_field_name` (text)
- `canonical_field_name` (text)
- `is_required` (bool)
- primary key: `(itsm_source, source_field_name)`

### `raw_snapshot`

- `id` (uuid)
- `itsm_source` (text)
- `snapshot_date` (timestamptz)
- `uploaded_by` (text)
- `uploaded_at` (timestamptz)
- `upload_metadata` (text/json)

### `snapshot_ticket`

- `id` (uuid)
- `snapshot_id` (uuid)
- `company_name` (text)
- `ticket_key` (text)

### `field_change`

- `id` (bigint, identity)
- `company_name` (text)
- `ticket_key` (text)
- `field_name` (text)
- `field_value` (text)
- `observed_at` (timestamptz)
- `snapshot_id` (uuid)

---

## Design Patterns / Rationale

- **Append-only history:** makes it easy to reconstruct past state accurately.
- **Roster table (`snapshot_ticket`):** avoids expensive reconstruction when answering “which tickets existed at time T?”
- **Canonical field names:** lets reports and UI stay stable even when source columns differ.

---

## Summary

If you want to consume UPMS data:

- Register `AddUpmsData(...)`
- Use `TicketDataService` / `TicketDataServiceInstance` to write snapshots and query point-in-time state
- Use ITSM source + field mapping services to standardise field names across sources
