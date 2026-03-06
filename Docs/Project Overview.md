# UPMS — Unified Problem Management System
**Project Overview**

---

## What Is UPMS?

UPMS is a **Unified Problem Management System** built for problem managers and service managers who work across multiple ITSM (IT Service Management) sources such as ServiceNow and Jira.

The core challenge it addresses is that problem managers typically work across several ITSM platforms simultaneously — and often across several *separate instances* of the same platform. Each instance stores ticket data differently — different field names, different structures, different export formats. UPMS provides a single place to ingest, normalise, store, and report on that data, regardless of which ITSM source it came from.

---

## Core Concepts

### 1. ITSM Sources — Named Instances, Not Tool Types

An **ITSM Source** is a **named, configured instance** of a ticketing tool — not merely the name of the tool itself. Two separate ServiceNow deployments used by the same team are **two different ITSM sources** in UPMS, because they may export different column names from their ticket tables.

Each ITSM Source definition contains:

- A **unique name** (slug-style identifier, e.g. `servicenow-client-a`, `jira-internal`) — this is the value stored in snapshot records
- A **display label** (human-readable, e.g. `"ServiceNow — Client A"`)
- A set of **field mappings**: each mapping says "in this source, the column named `X` maps to canonical name `Y`"
- A set of **required field names**: source column names that must be present in every uploaded CSV for this source — used for upload validation

ITSM sources are managed through the ITSM Source Management page in the web portal. See [ITSM Source Definition](ITSM_SOURCE_DEFINITION.md) for the full reference guide.

### 2. Field-Level Amendment Records (Append-Only History)

Rather than storing a full copy of a ticket each time it changes, UPMS stores **one record per field change per ticket per snapshot**. Each record says:

> "This field on this ticket had this value as of this timestamp."

This is the heart of the data model. Benefits include:

- **Point-in-time reconstruction** — any ticket can be reproduced as it appeared at any historical moment, by selecting the latest observed value for each field up to that point in time.
- **Minimal storage duplication** — only changes are stored, not repeated full copies of tickets.
- **Full audit trail** — every observed field value is preserved indefinitely.

### 3. Canonical Field Mapping

Different ITSM sources (and different instances of the same tool) use different field names for semantically identical data. For example:

| ITSM Source Instance | Source Column Name  | Canonical Name   |
|----------------------|---------------------|------------------|
| `servicenow-client-a`| `incident_state`    | `status`         |
| `jira-internal`      | `status`            | `status`         |
| `servicenow-client-a`| `assigned_to`       | `assigned_to`    |
| `jira-internal`      | `assignee`          | `assigned_to`    |
| `servicenow-client-a`| `short_description` | `title`          |

Each ITSM source definition holds its own field mapping table. During ingest, any column that has a mapping defined for that source is stored under its canonical name; any unmapped column is stored under its raw source column name. This allows data from different ITSM sources to be queried and reported against a consistent schema.

The full list of built-in canonical names is documented in [ITSM Source Definition](ITSM_SOURCE_DEFINITION.md).

### 4. Flat-Table CSV Ingest

When a user uploads a CSV file for an ITSM source:

- **Row 1 is the header row** — these are the actual column names from the ITSM export (e.g. `number`, `company`, `short_description`, `priority`, `state`)
- **Each subsequent row is one ticket** in its current state at the snapshot point in time
- **Company is read from the data** — it is not collected as upload metadata. The field mapping for the canonical name `company` on the selected ITSM source identifies which column in the CSV contains the company name for each ticket
- **Required field validation** — if any column name defined as required in the ITSM source definition is absent from the CSV header row, the upload is rejected with an error listing the missing fields

This flat-table design means one CSV file can contain tickets for multiple companies, as long as each row carries a company column.

### 5. Snapshots

A **snapshot** represents a batch of ticket data exported from an ITSM source at a specific point in time. Each snapshot records:

- Which ITSM source it came from (by name, referencing the ITSM source definition)
- The date and time the snapshot represents
- Who uploaded it and when

The company associated with each ticket comes from the ticket data itself (via the `company` canonical field mapping), not from the snapshot record.

Snapshots are the unit of ingest. When a file is uploaded, a snapshot record is created and the ticket field data within it is written as field change records.

---

## The Web Portal

UPMS is delivered as a **Blazor Server** web application (`UPMS.Web`). This is a single unified .NET application — there is no separate frontend SPA, no separate REST API server, and no separate background worker. Blazor Server handles both the server-side logic and the browser UI within one process.

The portal provides:

- **Snapshot management** — view uploaded snapshots and their contents
- **Ticket viewer** — browse and inspect individual tickets, including point-in-time state
- **Upload page** — ingest new CSV snapshot files; select an ITSM source, provide a snapshot date, and upload the file
- **ITSM Source Management** — add, edit, and delete ITSM source definitions and their field mappings
- **Report store** — access and run reporting plugins (see below)

---

## The Report Store and Plugin System

UPMS includes a **plugin-based reporting system** hosted within the web application. Report generators can be added as plugins without modifying the core application.

Each plugin:

- Declares its reporting options and parameter schema
- Defines the templates and output formats it supports
- Provides the generation logic for its reports

Examples of reporting plugins:

- **PowerPoint reporting pack** — generates templated slide decks where the user selects parameters (date range, company, styling options) and receives a populated PPTX file.
- **Email notification plugin** — gives users a choice of pre-defined email templates and generates HTML output ready to send.

The plugin system is extensible by design. New report types can be added as new plugins without changes to the core application.

> **Planned — not yet implemented.** The reporting plugin system is a planned feature. See [`Docs/Build Stages.md`](Build%20Stages.md) for implementation status.

---

## Intended Users

| Role | How They Use UPMS |
|------|-------------------|
| **Problem Manager** | Ingests snapshots from their ITSM tools, reviews ticket histories, generates management reports |
| **Service Manager** | Runs month-end reporting packs, reviews historical ticket state for service reviews |
| **Operations Team** | Audits ticket changes over time, validates data accuracy |
| **UPMS Administrator** | Defines and maintains ITSM source definitions and their field mappings |

---

## What UPMS Is Not

- **Not a live ITSM integration** — UPMS consumes exported snapshot files, not real-time API feeds from ITSM systems.
- **Not a real-time analytics engine** — data is accurate as of the last uploaded snapshot, not live.
- **Not a BI cube or dashboarding tool** — UPMS focuses on structured, templated reporting outputs rather than ad-hoc analytics or visualisation dashboards.
- **Not a replacement for your ITSM system** — UPMS sits alongside existing ITSM tools and consumes their data exports.

---

## Design Principles

- **Correctness first** — point-in-time reconstruction must be exact and deterministic.
- **Source-aware mapping** — field mappings belong to a specific named ITSM source instance, not to a tool type. Two instances of the same tool can have completely different mappings.
- **Flexibility** — the plugin system and canonical mapping mean new ITSM sources and new report types can be added without architectural changes.
- **Append-only history** — data is never modified after ingest; the full change log is preserved.
- **Simplicity of deployment** — a single Blazor Server application and a PostgreSQL database, runnable via Docker Compose.
