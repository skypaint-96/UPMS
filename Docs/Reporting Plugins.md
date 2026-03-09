# Reporting plugins and template runner

UPMS still uses a pluggable reporting contract, but the modern API/worker path is now **template-first**. In practice that means day-to-day report execution is centered on the shared `tokenised-template-report` runner and a curated library of uploaded templates rather than a menu of hard-coded concrete report definitions.

## Current direction

The report system is now split into two layers:

1. **Template type registry**
   - Defines which template formats the system supports.
   - Built from `IReportTemplateTypeProvider` registrations.
   - Supplies UX metadata such as display name, kind, extensions, inline-edit support, and authoring guidance.
2. **Template-driven report runner**
   - Reads one template from the shared library.
   - Resolves tokens from live ticket data.
   - Emits either preview HTML or a downloadable filled file.

This keeps the runtime extensible while removing the need to keep adding one-off report definitions for output formats that are better handled as templates.

## Core contract

Plugins still implement `IReportPlugin`:

- `PluginId`: stable identifier used by the API/UI.
- `DisplayName`: user-facing name.
- `Description`: user-facing description.
- `Parameters`: input definitions.
- `GenerateAsync(ReportRequest, CancellationToken)`: executes the report.

Outputs are returned as a `ReportResult`:

- `HtmlContent` for previews.
- or `FileName` + `ContentType` + `FileContent` for downloads.

## Modern API/worker registration

In the modern `UPMS.Reporting` service registration, the primary report plugin is now:

- **Template-Driven Report Generation** (`tokenised-template-report`)

The older concrete/example report classes still exist in the repository as reference material, but they are not the main runtime path for the API/worker architecture.

## Template type registry

Built-in template types currently include:

- HTML document
- HTML email
- EML draft
- Plain text document
- CSV spreadsheet
- XML payload
- Word document (`.docx`)
- Excel workbook (`.xlsx`)
- PowerPoint presentation (`.pptx`)

Each type declares metadata such as:

- report kind (`Document`, `Spreadsheet`, `Presentation`, `Email`, `Generic`)
- primary extension and allowed extensions
- content type
- rendering family (text / wordprocessing / spreadsheet / presentation)
- whether inline editing is supported
- authoring guidance for the UI
- starter template metadata/resource name

To add more supported types, register another `IReportTemplateTypeProvider`.

## Starter templates

Fresh environments now seed starter templates automatically from `src/UPMS.Reporting/StarterTemplates/`.

Seeding happens through `IReportTemplateBootstrapper` when the API and worker start. If a starter template already exists for a given type, it is left in place.

## Parameter types

`ReportParameterDefinition` continues to support the same general parameter model:

- `Text`
- `TextArea`
- `Date`
- `DateRange`
- `Select`
- `MultiSelect`
- `Boolean`
- `ItsmSource`

For the template runner, the most important parameters are:

- `template_id`
- `itsm_source`
- `company`
- `as_of_date`
- `ticket_keys`
- `detail_fields`
- `output_mode`

## Uploaded template library

The React **Report Templates** page is now a type-aware workspace for:

- reviewing supported template types
- starting from seeded example templates
- uploading new templates
- inline editing text-like templates
- updating metadata
- exporting/download existing templates
- deleting templates from the shared library

The **Reports** page then generates directly from this library, so the system revolves around selecting a template and filling it with parameters.

## Token syntax

Useful aggregate tokens include:

- `{{meta.company}}`
- `{{meta.itsm_source}}`
- `{{meta.as_of_date}}`
- `{{meta.generated_at_utc}}`
- `{{meta.requested_by}}`
- `{{kpi.ticket_count}}`
- `{{table.kpis.html}}`
- `{{output.tickets.csv_document}}`

Useful per-ticket tokens include:

- `{{ticket.Number}}`
- `{{ticket.State}}`
- `{{ticket.Priority}}`
- `{{ticket.Description}}`
- `{{ticket.Short_Description}}`
- `{{ticket.Assigned_To}}`
- `{{ticket.TicketKey}}`

Loop markers:

- `{{start per ticket <filter>}}`
- `{{start per ticket <filter> scope=page}}`
- `{{start per ticket <filter> scope=slide}}`
- `{{end per ticket}}`

Filter operators in the loop header support `=`, `!=`, `~`, and `!~`, and multiple conditions can be combined with `&&` or `;`.

## Authoring notes

- Text-like templates (`.html`, `.txt`, `.csv`, `.xml`, `.eml`) are best for inline authoring and rapid iteration.
- OOXML templates (`.docx`, `.xlsx`, `.pptx`) should be authored externally and uploaded as real files.
- Keep tokens visible as normal text in Word and PowerPoint.
- Whole-page or whole-slide duplication works best when loop markers are on their own paragraph/text box.
- The OOXML renderer still relies on direct XML text replacement, so keep tokens contiguous in the source file.
