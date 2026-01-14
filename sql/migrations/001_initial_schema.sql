-- ============================================================================
-- Migration: 001_initial_schema.sql
-- Purpose: Create core tables for Snapshot-Based Reporting & Analysis Platform
-- PostgreSQL 15+
-- ============================================================================

-- Enable UUID extension if not already enabled
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- ============================================================================
-- Table: raw_snapshot
-- Purpose: Stores snapshot metadata - when a snapshot was taken, from which
--          ITSM source, for which company, and who uploaded it.
-- ============================================================================
CREATE TABLE raw_snapshot (
    -- Primary key using UUID for distributed safety
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    
    -- Multi-tenant scoping - all queries should filter by company
    company_id      UUID NOT NULL,
    
    -- ITSM source identifier (e.g., 'servicenow', 'jira', 'zendesk')
    itsm_source     VARCHAR(100) NOT NULL,
    
    -- The business date/time this snapshot represents
    snapshot_date   TIMESTAMPTZ NOT NULL,
    
    -- Upload audit information
    uploaded_by     VARCHAR(255) NOT NULL,
    uploaded_at     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    
    -- Optional metadata about the upload (filename, row count, etc.)
    upload_metadata JSONB,
    
    -- Audit column
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    
    -- Future partitioning hint: PARTITION BY RANGE (snapshot_date)
    -- or PARTITION BY LIST (company_id) for large multi-tenant deployments
    
    CONSTRAINT chk_snapshot_date_not_future CHECK (snapshot_date <= NOW() + INTERVAL '1 day')
);

COMMENT ON TABLE raw_snapshot IS 'Stores metadata about each imported snapshot from ITSM systems';
COMMENT ON COLUMN raw_snapshot.company_id IS 'Multi-tenant identifier - all queries must scope by company';
COMMENT ON COLUMN raw_snapshot.itsm_source IS 'Source system identifier (servicenow, jira, etc.)';
COMMENT ON COLUMN raw_snapshot.snapshot_date IS 'Business date/time the snapshot represents';
COMMENT ON COLUMN raw_snapshot.upload_metadata IS 'Optional JSON metadata: filename, row_count, validation_status';

-- ============================================================================
-- Table: snapshot_ticket
-- Purpose: Lightweight index mapping tickets to snapshots they appeared in.
--          Enables fast snapshot → ticket lookup without expensive DISTINCT scans.
-- ============================================================================
CREATE TABLE snapshot_ticket (
    -- Composite primary key: a ticket appears once per snapshot
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    
    -- Foreign key to the snapshot
    snapshot_id     UUID NOT NULL REFERENCES raw_snapshot(id) ON DELETE CASCADE,
    
    -- Multi-tenant scoping (denormalized for query efficiency)
    company_id      UUID NOT NULL,
    
    -- Business identifier from the source ITSM system
    -- VARCHAR to accommodate various key formats (INC0001234, JIRA-5678, etc.)
    ticket_key      VARCHAR(255) NOT NULL,
    
    -- Audit column
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    
    -- Ensure a ticket only appears once per snapshot
    CONSTRAINT uq_snapshot_ticket UNIQUE (snapshot_id, ticket_key),
    
    -- Future partitioning hint: PARTITION BY HASH (snapshot_id) for parallel scans
    -- or PARTITION BY LIST (company_id) for tenant isolation
    
    CONSTRAINT fk_snapshot_ticket_company CHECK (company_id IS NOT NULL)
);

COMMENT ON TABLE snapshot_ticket IS 'Index mapping: which tickets appeared in which snapshot';
COMMENT ON COLUMN snapshot_ticket.ticket_key IS 'Business identifier from source ITSM (e.g., INC0001234)';
COMMENT ON COLUMN snapshot_ticket.company_id IS 'Denormalized for efficient company-scoped queries';

-- ============================================================================
-- Table: field_change
-- Purpose: Source of truth for ticket state. Stores atomic field-level changes.
--          Each record represents: "This field for this ticket had this value 
--          as of this time." Enables exact point-in-time reconstruction.
-- ============================================================================
CREATE TABLE field_change (
    -- Primary key using BIGSERIAL for high-volume inserts
    -- UUID alternative: id UUID PRIMARY KEY DEFAULT uuid_generate_v4()
    id              BIGSERIAL PRIMARY KEY,
    
    -- Multi-tenant scoping (denormalized for query efficiency)
    company_id      UUID NOT NULL,
    
    -- Business identifier from the source ITSM system
    ticket_key      VARCHAR(255) NOT NULL,
    
    -- The field being tracked (e.g., 'Status', 'Priority', 'Assignee')
    field_name      VARCHAR(255) NOT NULL,
    
    -- The value as TEXT for flexible schema
    -- NULL represents explicit null/empty values
    field_value     TEXT,
    
    -- When this field value was observed (from snapshot_date)
    -- Critical for point-in-time reconstruction
    observed_at     TIMESTAMPTZ NOT NULL,
    
    -- Link to the snapshot this change came from
    snapshot_id     UUID NOT NULL REFERENCES raw_snapshot(id) ON DELETE CASCADE,
    
    -- Audit column
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    
    -- Future partitioning hint: PARTITION BY RANGE (observed_at)
    -- This is the most likely partitioning strategy for time-series reconstruction
    
    CONSTRAINT chk_field_name_not_empty CHECK (field_name <> ''),
    CONSTRAINT chk_ticket_key_not_empty CHECK (ticket_key <> '')
);

COMMENT ON TABLE field_change IS 'Source of truth: atomic field-level changes for point-in-time reconstruction';
COMMENT ON COLUMN field_change.ticket_key IS 'Business identifier from source ITSM';
COMMENT ON COLUMN field_change.field_name IS 'Name of the field (Status, Priority, Assignee, etc.)';
COMMENT ON COLUMN field_change.field_value IS 'Field value as TEXT - NULL represents explicit null';
COMMENT ON COLUMN field_change.observed_at IS 'Timestamp when this value was observed - key for reconstruction';
COMMENT ON COLUMN field_change.snapshot_id IS 'Source snapshot for traceability';

-- ============================================================================
-- End of initial schema migration
-- ============================================================================
