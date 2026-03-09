# API Core Requirements

## Scope

These requirements define the first-class API needed to replace the current direct coupling between UI and server-side Blazor logic.

## Functional requirements

### AP-001 — Health and discoverability
The API shall expose a health endpoint and an OpenAPI surface for development and integration discovery.

### AP-002 — Canonical field catalog access
The API shall expose canonical field definitions so UI and external consumers can understand the reporting/data model.

### AP-003 — ITSM source discovery
The API shall expose ITSM source summaries and source-detail mappings.

### AP-004 — ITSM source maintenance
The API shall support creation of ITSM sources and maintenance of source field mappings.

### AP-005 — Snapshot retrieval
The API shall return snapshot collections filtered by ITSM source and optionally company.

### AP-006 — Snapshot detail retrieval
The API shall expose individual snapshot records and the ticket roster reconstructed for that snapshot.

### AP-007 — Ticket retrieval by as-of time
The API shall expose ticket queries by ITSM source, optional company, and optional field filter at a given as-of timestamp.

### AP-008 — Ticket detail retrieval
The API shall return the reconstructed field state for a specific ticket.

### AP-009 — Ticket history retrieval
The API shall return historical field changes for a ticket.

### AP-010 — Synchronous ingest endpoint
The API shall support direct CSV/JSON ingest for smaller or controlled workflows.

### AP-011 — Report plugin discovery
The API shall expose report plugin metadata and parameter definitions.

### AP-012 — Synchronous report execution
The API shall support immediate report execution for lightweight reports and return either JSON preview or file output.

## Non-functional requirements

### AP-101 — Versioned routing
All business endpoints shall be hosted under `/api/v1`.

### AP-102 — Backend portability
The API shall remain on `.NET 10` and reuse the existing shared data model instead of forking domain logic.

### AP-103 — External consumer friendliness
The API shall return stable JSON payloads suitable for Power Query, scripting, or downstream integration.

### AP-104 — Secure external exposure
The API shall support a mode where access is protected by an API key header.

### AP-105 — Container-first runtime
The API shall be packaged for container hosting and able to run alongside PostgreSQL, worker, and frontend.
