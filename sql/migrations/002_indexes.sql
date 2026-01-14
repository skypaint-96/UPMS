-- ============================================================================
-- Migration: 002_indexes.sql
-- Purpose: Create indexes for efficient queries on the snapshot reporting tables
-- PostgreSQL 15+
-- ============================================================================

-- ============================================================================
-- Indexes on raw_snapshot
-- ============================================================================

-- Company scoping: All queries filter by company_id first
CREATE INDEX idx_raw_snapshot_company_id 
    ON raw_snapshot (company_id);

-- Snapshot lookup by company and date range (common reporting pattern)
CREATE INDEX idx_raw_snapshot_company_date 
    ON raw_snapshot (company_id, snapshot_date DESC);

-- ITSM source filtering within a company
CREATE INDEX idx_raw_snapshot_company_itsm 
    ON raw_snapshot (company_id, itsm_source);

COMMENT ON INDEX idx_raw_snapshot_company_id IS 'Multi-tenant filtering - first predicate in most queries';
COMMENT ON INDEX idx_raw_snapshot_company_date IS 'Efficient date range queries within a company';
COMMENT ON INDEX idx_raw_snapshot_company_itsm IS 'Filter snapshots by source system';

-- ============================================================================
-- Indexes on snapshot_ticket
-- ============================================================================

-- Primary lookup: Get all tickets for a specific snapshot
-- This is the main use case for SnapshotTicket table
CREATE INDEX idx_snapshot_ticket_snapshot_id 
    ON snapshot_ticket (snapshot_id);

-- Company scoping for direct ticket lookups
CREATE INDEX idx_snapshot_ticket_company_id 
    ON snapshot_ticket (company_id);

-- Find all snapshots containing a specific ticket (reverse lookup)
CREATE INDEX idx_snapshot_ticket_ticket_key 
    ON snapshot_ticket (ticket_key, company_id);

-- Composite: Efficient lookup for "which snapshots contain this ticket for this company"
CREATE INDEX idx_snapshot_ticket_company_ticket 
    ON snapshot_ticket (company_id, ticket_key);

COMMENT ON INDEX idx_snapshot_ticket_snapshot_id IS 'Fast snapshot → tickets lookup (primary use case)';
COMMENT ON INDEX idx_snapshot_ticket_ticket_key IS 'Reverse lookup: find snapshots containing a ticket';
COMMENT ON INDEX idx_snapshot_ticket_company_ticket IS 'Company-scoped ticket history across snapshots';

-- ============================================================================
-- Indexes on field_change
-- ============================================================================

-- CRITICAL: Ticket reconstruction index
-- This is the most important index for point-in-time reconstruction
-- Supports: "Get all field values for ticket X as of time T"
CREATE INDEX idx_field_change_ticket_reconstruction 
    ON field_change (ticket_key, observed_at DESC);

-- Company-scoped ticket reconstruction
-- Ensures tenant isolation in queries
CREATE INDEX idx_field_change_company_ticket_time 
    ON field_change (company_id, ticket_key, observed_at DESC);

-- Snapshot-based lookups
-- Supports: "Get all field changes from snapshot X"
CREATE INDEX idx_field_change_snapshot_id 
    ON field_change (snapshot_id);

-- Company scoping for bulk operations
CREATE INDEX idx_field_change_company_id 
    ON field_change (company_id);

-- Composite: Company + observed_at for time-range queries across all tickets
-- Useful for "What changed in the last 30 days for company X"
CREATE INDEX idx_field_change_company_time 
    ON field_change (company_id, observed_at DESC);

-- Field-specific lookups (optional, enable if needed)
-- Supports: "History of Status field changes for ticket X"
CREATE INDEX idx_field_change_ticket_field 
    ON field_change (ticket_key, field_name, observed_at DESC);

-- COVERING INDEX for reconstruction queries (PostgreSQL 11+)
-- Includes field_value to enable index-only scans for common reconstruction patterns
CREATE INDEX idx_field_change_reconstruction_covering 
    ON field_change (company_id, ticket_key, field_name, observed_at DESC) 
    INCLUDE (field_value);

COMMENT ON INDEX idx_field_change_ticket_reconstruction IS 'CRITICAL: Point-in-time ticket reconstruction';
COMMENT ON INDEX idx_field_change_company_ticket_time IS 'Company-scoped reconstruction with tenant isolation';
COMMENT ON INDEX idx_field_change_snapshot_id IS 'Lookup all changes from a specific snapshot';
COMMENT ON INDEX idx_field_change_company_time IS 'Time-range queries across all tickets in a company';
COMMENT ON INDEX idx_field_change_ticket_field IS 'Field-specific history for a ticket';
COMMENT ON INDEX idx_field_change_reconstruction_covering IS 'Covering index for index-only reconstruction scans';

-- ============================================================================
-- Partial Indexes (optional optimizations)
-- ============================================================================

-- If certain fields are queried more frequently, consider partial indexes:
-- Example: Index only Status field changes (uncomment if needed)
-- CREATE INDEX idx_field_change_status_only 
--     ON field_change (company_id, ticket_key, observed_at DESC) 
--     WHERE field_name = 'Status';

-- ============================================================================
-- Statistics targets (for query planner optimization)
-- ============================================================================

-- Increase statistics for frequently filtered columns
ALTER TABLE field_change ALTER COLUMN company_id SET STATISTICS 1000;
ALTER TABLE field_change ALTER COLUMN ticket_key SET STATISTICS 1000;
ALTER TABLE field_change ALTER COLUMN field_name SET STATISTICS 500;

ALTER TABLE snapshot_ticket ALTER COLUMN company_id SET STATISTICS 1000;
ALTER TABLE snapshot_ticket ALTER COLUMN ticket_key SET STATISTICS 1000;

ALTER TABLE raw_snapshot ALTER COLUMN company_id SET STATISTICS 1000;

COMMENT ON TABLE field_change IS 'Run ANALYZE after bulk loads to update statistics';

-- ============================================================================
-- End of indexes migration
-- ============================================================================
