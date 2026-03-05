-- ============================================================================
-- Stored Procedure: get_tickets_for_snapshot
-- Purpose: Get all ticket keys that appeared in a specific snapshot
-- PostgreSQL 15+
-- ============================================================================

-- ============================================================================
-- Function: get_tickets_for_snapshot
-- 
-- Retrieves all ticket keys associated with a given snapshot.
-- Uses the snapshot_ticket table for efficient lookup (avoids DISTINCT scans).
--
-- Parameters:
--   p_snapshot_id - UUID of the snapshot
--
-- Returns:
--   Table of ticket_key values
--
-- Example:
--   SELECT * FROM get_tickets_for_snapshot('snapshot-uuid'::uuid);
-- ============================================================================

CREATE OR REPLACE FUNCTION get_tickets_for_snapshot(
    p_snapshot_id UUID
)
RETURNS TABLE (
    ticket_key VARCHAR(255)
)
LANGUAGE SQL
STABLE
PARALLEL SAFE
AS $$
    SELECT st.ticket_key
    FROM snapshot_ticket st
    WHERE st.snapshot_id = p_snapshot_id
    ORDER BY st.ticket_key;
$$;

COMMENT ON FUNCTION get_tickets_for_snapshot(UUID) IS 
    'Returns all ticket keys for a snapshot using the efficient snapshot_ticket index.';

-- ============================================================================
-- Function: get_tickets_for_snapshot_paged
-- 
-- Paginated version for large snapshots. Supports cursor-based pagination
-- for efficient streaming of large result sets.
--
-- Parameters:
--   p_snapshot_id  - UUID of the snapshot
--   p_page_size    - Number of tickets per page (default 1000)
--   p_offset       - Number of tickets to skip (for offset pagination)
--
-- Returns:
--   Table of ticket_key values
--
-- Example:
--   -- First page
--   SELECT * FROM get_tickets_for_snapshot_paged('snapshot-uuid'::uuid, 1000, 0);
--   -- Second page
--   SELECT * FROM get_tickets_for_snapshot_paged('snapshot-uuid'::uuid, 1000, 1000);
-- ============================================================================

CREATE OR REPLACE FUNCTION get_tickets_for_snapshot_paged(
    p_snapshot_id UUID,
    p_page_size   INTEGER DEFAULT 1000,
    p_offset      INTEGER DEFAULT 0
)
RETURNS TABLE (
    ticket_key VARCHAR(255)
)
LANGUAGE SQL
STABLE
PARALLEL SAFE
AS $$
    SELECT st.ticket_key
    FROM snapshot_ticket st
    WHERE st.snapshot_id = p_snapshot_id
    ORDER BY st.ticket_key
    LIMIT p_page_size
    OFFSET p_offset;
$$;

COMMENT ON FUNCTION get_tickets_for_snapshot_paged(UUID, INTEGER, INTEGER) IS 
    'Paginated ticket listing for streaming large snapshots.';

-- End of get_tickets_for_snapshot stored procedure
