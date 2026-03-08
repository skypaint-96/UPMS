# Starter report templates

The project now ships with a small starter pack under `src/UPMS.Web/App_Data/ReportTemplates/` so power users can immediately test the tokenised reporting flow without building templates from scratch.

## Included templates

- **Starter HTML - Operations Brief**
  - KPI cards for company, source, ticket count, and backlog.
  - Repeats one ticket card per matching ticket using `{{start per ticket ...}}`.
- **Starter Word - Operations Summary**
  - Summary page followed by one repeated page per matching ticket.
  - Loop markers are kept on their own paragraphs because that is the most reliable structure for DOCX page duplication.
- **Starter PowerPoint - Ticket Deck**
  - Executive summary cover slide followed by one repeated slide per active ticket.
  - Uses whole-slide per-ticket markers so the renderer can duplicate the slide cleanly.

## Example token patterns

Aggregate tokens:

- `{{meta.company}}`
- `{{meta.itsm_source}}`
- `{{meta.as_of_date}}`
- `{{kpi.ticket_count}}`
- `{{kpi.month_end.backlog_current}}`
- `{{kpi.month_end.high_priority_backlog_current}}`

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
- `{{start per ticket State!=Closed scope=page}}`
- `{{end per ticket}}`

## Authoring notes

- Keep Word and PowerPoint tokens as normal visible text instead of hiding them in comments or metadata.
- In Word, put page-level loop markers on their own paragraphs.
- In PowerPoint, put slide-level loop markers on the slide that should be duplicated.
- For canonical fields such as **Company**, the updated UI now offers suggestion lists when the field type is known and the current ITSM source has data.
