# Technical Architecture

## High-Level Architecture
The system is composed of five layers:

1. **Ingest Layer** – accepts snapshot exports  
2. **Persistence Layer (Database)** – stores historical state efficiently  
3. **Data Access Layer** – exposes stable, time-aware queries  
4. **Reporting Core** – orchestrates reports in a format-agnostic way  
5. **Rendering Plugins** – produce specific outputs (Excel, PPTX, Email)

Each layer has a single responsibility and communicates through clear contracts.

---

## Persistence Model

### FieldChange (Source of Truth)
Stores atomic field-level changes.

Each record represents:
> “This field for this ticket had this value as of this time.”

Benefits:
- Exact point-in-time reconstruction
- Minimal storage duplication
- Full auditability

### SnapshotTicket
A lightweight index mapping:
> “Which tickets appeared in which snapshot.”

Benefits:
- Fast snapshot → ticket lookup
- Efficient paging for large reports
- Avoids expensive DISTINCT scans

### RawSnapshot
Stores snapshot metadata:
- ITSM source
- Company
- Snapshot date
- Upload information

---

## Database Responsibilities
The database:
- stores all historical data,
- ensures deterministic ordering,
- exposes **stored procedures** as the main read interface.

Key stored procedures:
- tickets as of a given time,
- fields for a ticket at a time,
- batch reconstruction for reporting.

The database performs **set-based operations** (windowing, grouping).  
Business logic remains in the application.

---

## Reporting Core
`Reporting.Core` defines:
- report requests and parameters,
- streaming row models,
- renderer contracts.

It:
- validates parameters,
- pages through data via stored procedures,
- exposes results as streams.

It does **not** know about Excel, PowerPoint, or email.

---

## Rendering Plugins
Each output format is a plugin:
- Excel / CSV
- PowerPoint (template-based)
- Email (HTML)
- PDF (optional)

Plugins:
- declare supported actions (download, email, etc.),
- consume generic report rows,
- apply formatting via templates.

No plugin accesses raw tables directly.

---

## Templates & Presentation
Templates are external assets (PPTX, XLSX, HTML) that:
- contain placeholders (tokens, named ranges, alt-text),
- preserve designer-defined styling,
- are registered and versioned separately from code.

A template registrar allows:
- uploading templates,
- previewing placeholders,
- binding templates to reports and roles.

---

## Web UI
The UI:
- lists registered reports,
- dynamically renders parameters,
- exposes actions defined by plugins.

The UI does not encode report logic.

---

## Architectural Principles
- Append-only history  
- Deterministic reconstruction  
- Separation of data, logic, and presentation  
- Extensibility over premature optimisation  
- Templates over hard-coded layouts  
