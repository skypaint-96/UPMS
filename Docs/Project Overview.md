# UPMS — Unified Problem Management System
**Project Overview**

---

## What Is UPMS?

UPMS is a **Unified Problem Management System** built for problem managers and service managers who work across multiple ITSM (IT Service Management) sources such as ServiceNow and Jira.

The core challenge it addresses is that problem managers typically work across several ITSM platforms simultaneously. Each platform stores ticket data differently — different field names, different structures, different export formats. UPMS provides a single place to ingest, normalise, store, and report on that data, regardless of which ITSM source it came from.

---

## Core Concepts

### 1. Field-Level Amendment Records (Append-Only History)

Rather than storing a full copy of a ticket each time it changes, UPMS stores **one record per field change per ticket per snapshot**. Each record says:

> "This field on this ticket had this value as of this timestamp."

This is the heart of the data model. Benefits include:

- **Point-in-time reconstruction** — any ticket can be reproduced as it appeared at any historical moment, by selecting the latest observed value for each field up to that point in time.
- **Minimal storage duplication** — only changes are stored, not repeated full copies of tickets.
- **Full audit trail** — every observed field value is preserved indefinitely.

### 2. Canonical Field Mapping

Different ITSM sources use different field names for semantically identical data. For example:

| ITSM Source | Source Field Name | Canonical Name |
|-------------|-------------------|----------------|
| ServiceNow  | `incident_state`  | `Status`       |
| Jira        | `status`          | `Status`       |
| ServiceNow  | `assigned_to`     | `Assignee`     |
| Jira        | `assignee`        | `Assignee`     |

UPMS maintains a **reference mapping table** (`itsm_field_mapping`) that maps source-specific field names to canonical names. During ingest, source field names are translated to their canonical equivalents so that data from different ITSM sources can be queried and reported against a consistent schema.

> **Planned — not yet implemented.** The canonical field mapping table and mapping service are on the roadmap. See [`Docs/Build Stages.md`](Build%20Stages.md) for implementation status.

### 3. Snapshots

A **snapshot** represents a batch of ticket data exported from an ITSM source at a specific point in time. Each snapshot records:

- Which ITSM source it came from
- The date and time the snapshot represents
- Which company's data it contains
- Who uploaded it and when

Snapshots are the unit of ingest. When a file is uploaded, a snapshot record is created and the ticket field data within it is written as field change records.

---

## The Web Portal

UPMS is delivered as a **Blazor Server** web application (`UPMS.Web`). This is a single unified .NET application — there is no separate frontend SPA, no separate REST API server, and no separate background worker. Blazor Server handles both the server-side logic and the browser UI within one process.

The portal provides:

- **Snapshot management** — view uploaded snapshots and their contents
- **Ticket viewer** — browse and inspect individual tickets, including point-in-time state
- **Upload page** — ingest new snapshot files
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

---

## What UPMS Is Not

- **Not a live ITSM integration** — UPMS consumes exported snapshot files, not real-time API feeds from ITSM systems.
- **Not a real-time analytics engine** — data is accurate as of the last uploaded snapshot, not live.
- **Not a BI cube or dashboarding tool** — UPMS focuses on structured, templated reporting outputs rather than ad-hoc analytics or visualisation dashboards.
- **Not a replacement for your ITSM system** — UPMS sits alongside existing ITSM tools and consumes their data exports.

---

## Design Principles

- **Correctness first** — point-in-time reconstruction must be exact and deterministic.
- **Flexibility** — the plugin system and canonical mapping mean new ITSM sources and new report types can be added without architectural changes.
- **Append-only history** — data is never modified after ingest; the full change log is preserved.
- **Simplicity of deployment** — a single Blazor Server application and a PostgreSQL database, runnable via Docker Compose.
