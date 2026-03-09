# Starter report templates

The modern API/worker reporting flow now seeds a starter pack from `src/UPMS.Reporting/StarterTemplates/` into the configured report-template storage on startup. This means a fresh environment immediately has example templates available in both the **Reports** page and the **Report Templates** workspace.

## Included starter types

Fresh state now includes one seeded starter for each supported built-in template type:

- **HTML document** (`html-document`, `.html`)
  - Operations brief with KPI cards and repeated ticket sections.
- **HTML email** (`html-email`, `.html`)
  - Update email that can generate a downloadable `.eml` draft in Auto mode.
- **EML draft** (`eml-email`, `.eml`)
  - Raw RFC822 email shell with tokenised headers and body.
- **Plain text document** (`text-document`, `.txt`)
  - Text-first operational summary with repeated ticket sections.
- **CSV spreadsheet** (`csv-spreadsheet`, `.csv`)
  - Export-friendly ticket register with looped ticket rows.
- **XML payload** (`xml-generic`, `.xml`)
  - Structured payload with repeated `<ticket>` fragments.
- **Word document** (`word-document`, `.docx`)
  - Summary page followed by one repeated page-style section per ticket.
- **Excel workbook** (`excel-workbook`, `.xlsx`)
  - Workbook with summary/helper sheets for spreadsheet consumers.
- **PowerPoint presentation** (`powerpoint-presentation`, `.pptx`)
  - Cover slide plus one repeated slide per ticket.

## How starter seeding works

- Supported template types come from the `ReportTemplateTypeRegistry`.
- Each type can declare starter metadata plus an embedded resource name.
- `IReportTemplateBootstrapper` seeds any missing starter templates when the API or worker starts.
- If a starter template already exists for a type, it is left in place.
- Additional supported types can be introduced by registering more `IReportTemplateTypeProvider` implementations.

## Example token patterns

Aggregate tokens:

- `{{meta.company}}`
- `{{meta.itsm_source}}`
- `{{meta.as_of_date}}`
- `{{meta.generated_at_utc}}`
- `{{kpi.ticket_count}}`
- `{{table.kpis.html}}`
- `{{output.tickets.csv_document}}`

Per-ticket tokens:

- `{{ticket.Number}}`
- `{{ticket.Short_Description}}`
- `{{ticket.Company}}`
- `{{ticket.State}}`
- `{{ticket.Priority}}`
- `{{ticket.Assigned_To}}`
- `{{ticket.Assignment_Group}}`
- `{{ticket.Description}}`
- `{{ticket.Workaround}}`

Loop markers:

- `{{start per ticket State!=Closed}}`
- `{{start per ticket Priority=High scope=page}}`
- `{{start per ticket State!=Closed scope=slide}}`
- `{{end per ticket}}`

## Authoring notes

- Text-like types (`.html`, `.txt`, `.csv`, `.xml`, `.eml`) can be edited inline from the React template workspace.
- OOXML types (`.docx`, `.xlsx`, `.pptx`) should be uploaded as real files authored externally.
- Keep Word and PowerPoint loop markers as visible text, ideally on their own paragraph or slide text box when duplicating whole pages/slides.
- The **Reports** page now generates directly from the shared template library instead of relying on a separate set of hard-coded concrete report definitions.
