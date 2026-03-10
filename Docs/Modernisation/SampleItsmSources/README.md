# Sample ITSM source pack

These files are for manual testing of source import and snapshot ingest.

## Suggested order

1. Import one of the source definition files from the **Settings -> ITSM Sources** page.
2. For the CSV source definition files (`jira-modern.csv` and `jira-legacy.csv`), enter a **Source name override** and **Display label override** in the form before importing, because CSV definitions do not carry source metadata.
3. Upload the matching snapshot CSV from **Upload Snapshot**.
4. Review the imported source mappings, snapshot, tickets, and jobs.

## Included source definitions

- `servicenow-modern.json`
- `servicenow-legacy.json`
- `jira-modern.csv`
- `jira-legacy.csv`
- `remedy-modern.json`

## Included snapshots

- `servicenow-snapshot.csv`
- `jira-snapshot.csv`
- `remedy-snapshot.csv`

The `legacy` definitions intentionally use the older canonical aliases such as `ticket_key`, `company`, `title`, and `status` so you can verify backward-compatible import behaviour.
