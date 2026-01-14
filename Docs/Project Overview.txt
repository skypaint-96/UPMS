# Snapshot-Based Reporting & Analysis Platform  
**Project Overview (Non-Technical)**

## Purpose
This project aims to build a flexible reporting platform that can accurately show **how ITSM tickets looked at any point in time**, using snapshot data exported from multiple ITSM systems.

Rather than treating reports as static outputs, the platform treats reports as **reusable views over historical state**, allowing users to:
- select a date (e.g. month-end),
- select what they want to see (metrics, ticket details, summaries),
- and generate different outputs (Excel, PowerPoint, email summaries) from the same underlying data.

## The Problem Being Solved
Traditional reporting approaches:
- store full copies of tickets per snapshot,
- unflexable field schemas,
- tightly couple reports to export formats,
- and require bespoke logic per report type.

This leads to:
- high storage costs,
- poor traceability,
- duplicated logic across reports,
- difficulty evolving reports over time.

This project solves that by:
- storing **only what changed**, when it changed,
- reconstructing ticket state on demand,
- separating **data**, **report logic**, and **presentation**.

## What the Platform Enables
From the same dataset, users can:
- generate a month-end snapshot for a company,
- view how a specific ticket looked on a given date,
- produce management metrics decks,
- generate detailed ticket appendices,
- reuse templates without code changes.

Reports are **highly extensible**: new report types and output formats can be added without redesigning the system.

## Intended Users
- Operational teams needing historical accuracy  
- Service managers producing monthly reports  
- Engineers needing an auditable reporting pipeline  
- Future automation or analytics tooling  

## What This Is Not
- Not a live ITSM integration platform  
- Not a real-time analytics engine  
- Not a BI cube or dashboarding tool  
- Not focused on heavy caching upfront  


The emphasis is on **correctness, flexibility, and reuse**, with performance optimised only when needed.
