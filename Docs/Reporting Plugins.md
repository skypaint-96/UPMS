# Reporting Plugins

UPMS exposes a **report store** under `/reports` that discovers and runs **reporting plugins**.

A plugin is a normal C# class (registered with DI) that:

1. Describes its inputs (parameters)
2. Generates an output (HTML preview or a downloadable file)

This keeps the core app small while allowing new reports to be added without changing the UI.

---

## Goals

- Keep report generation **pluggable** (add/remove a report by registering a class)
- Support both:
  - **Preview reports** (HTML rendered in the browser)
  - **Download reports** (PPTX/CSV/whatever)
- Keep the contract simple enough that writing a new report is “just implement an interface”

Non-goals (for now):

- Background scheduling / report jobs
- Multi-tenant access control beyond the web app’s normal auth
- Streaming very large outputs

---

## Core Contract

Plugins implement `IReportPlugin`:

- `PluginId`: stable identifier (used by the UI)
- `DisplayName`: user-facing name
- `Description`: user-facing description
- `Parameters`: a list of `ReportParameterDefinition` describing what the report needs
- `GenerateAsync(ReportRequest, CancellationToken)`: does the work and returns `ReportResult`

Outputs are returned as a `ReportResult`:

- `HtmlContent` (for previews)
- or `FileName` + `ContentType` + `FileContent` (for downloads)

---

## Parameter Types

Each report defines parameters using `ReportParameterDefinition`.

Supported parameter types (`ReportParameterType`):

- `Text`: free text
- `Date`: a date (rendered as an `<input type="date">`)
- `DateRange`: a pair of dates. The form writes **two keys**:
  - `<key>_from`
  - `<key>_to`
- `Select`: a dropdown from fixed options
- `MultiSelect`: rendered as checkboxes
  - Values are stored as a **comma-separated string** in `ReportRequest.Parameters[key]`
- `Boolean`: a checkbox (`"true"`/`"false"`)
- `ItsmSource`: a dropdown populated dynamically from the `itsm_source` table

Notes:

- Plugins should treat all parameter values as **strings** and parse/validate internally.
- For user-defined ITSM sources, prefer `ItsmSource` over a hard-coded `Select`.

---

## Plugin Registration

Plugins are discovered via DI. In `src/UPMS.Web/Program.cs`:

```csharp
builder.Services.AddScoped<IReportPlugin, MyReportPlugin>();
```

The report store queries a `PluginRegistry` which collects all registered `IReportPlugin` implementations.

---

## Implementing a New Plugin

Minimum skeleton:

```csharp
public sealed class MyReportPlugin : IReportPlugin
{
    public string PluginId => "my-report";
    public string DisplayName => "My Report";
    public string Description => "Does something useful.";

    public IReadOnlyList<ReportParameterDefinition> Parameters =>
    [
        new() { Key = "itsm_source", DisplayName = "ITSM Source", Type = ReportParameterType.ItsmSource, IsRequired = true },
        new() { Key = "company", DisplayName = "Company", Type = ReportParameterType.Text, IsRequired = true },
        new() { Key = "as_of_date", DisplayName = "As Of Date", Type = ReportParameterType.Date, IsRequired = true },
    ];

    public async Task<ReportResult> GenerateAsync(ReportRequest request, CancellationToken ct = default)
    {
        // 1) Validate
        // 2) Query data (TicketDataServiceInstance)
        // 3) Produce output
        return new ReportResult { Success = true, OutputType = ReportOutputType.PlainText, HtmlContent = "Hello" };
    }
}
```

### Data access

Most report plugins will consume `TicketDataServiceInstance` (scoped) and use:

- `GetTicketsAsync(itsmSource, company, asOfDate)`
- `GetSnapshotsAsync(itsmSource, company)`
- `GetTicketsBySnapshotAsync(snapshotId)`

---

## Testing Plugins

The project contains unit tests for plugins under `tests/UPMS.Web.Tests`.

Patterns used by the current tests:

- Use an **in-memory SQLite** database
- Create the minimal required tables (`raw_snapshot`, `snapshot_ticket`, `field_change`)
- Construct `UpmsDbContext` + `TicketDataService` + `TicketDataServiceInstance`
- Run the plugin and assert on `ReportResult`

Example tests are included for the example plugins:

- `ExampleStatusBreakdownPluginTests`
- `ExampleTicketCsvExportPluginTests`
- `ExampleFieldDeltaReportPluginTests`

---

## Included Plugins

### Core

- **Stub Report** (`stub-plugin`)
  - Used for smoke-testing the plugin infrastructure.

### Production-ish examples

- **PowerPoint Report Pack** (`powerpoint-report`)
  - Generates a minimal PPTX summary download.

- **Email Notification** (`email-notification`)
  - Generates an HTML email preview from a selected template.

### Example plugins

- **Status Breakdown (Example)** (`example-status-breakdown`)
  - HTML breakdown of ticket counts by Status (or another field)

- **Ticket CSV Export (Example)** (`example-ticket-csv-export`)
  - CSV download of tickets as-of a date

- **Field Delta (Example)** (`example-field-delta`)
  - Compare a chosen field between two dates and report new/removed/changed tickets
