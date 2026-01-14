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

-- ============================================================================
-- Function: get_tickets_for_snapshot_cursor
-- 
-- Cursor-based pagination (keyset pagination) for better performance on
-- large datasets. More efficient than OFFSET for deep pagination.
--
-- Parameters:
--   p_snapshot_id      - UUID of the snapshot
--   p_page_size        - Number of tickets per page
--   p_last_ticket_key  - Last ticket_key from previous page (NULL for first page)
--
-- Returns:
--   Table of ticket_key values starting after the cursor
--
-- Example:
--   -- First page
--   SELECT * FROM get_tickets_for_snapshot_cursor('snapshot-uuid'::uuid, 1000, NULL);
--   -- Next page (use last ticket_key from previous result)
--   SELECT * FROM get_tickets_for_snapshot_cursor('snapshot-uuid'::uuid, 1000, 'INC0001000');
-- ============================================================================

CREATE OR REPLACE FUNCTION get_tickets_for_snapshot_cursor(
    p_snapshot_id      UUID,
    p_page_size        INTEGER DEFAULT 1000,
    p_last_ticket_key  VARCHAR(255) DEFAULT NULL
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
      AND (p_last_ticket_key IS NULL OR st.ticket_key > p_last_ticket_key)
    ORDER BY st.ticket_key
    LIMIT p_page_size;
$$;

COMMENT ON FUNCTION get_tickets_for_snapshot_cursor(UUID, INTEGER, VARCHAR) IS 
    'Cursor-based (keyset) pagination for efficient deep pagination of tickets.';

-- ============================================================================
-- Function: get_snapshot_ticket_count
-- 
-- Returns the total count of tickets in a snapshot.
-- Useful for progress reporting and pagination calculations.
--
-- Parameters:
--   p_snapshot_id - UUID of the snapshot
--
-- Returns:
--   BIGINT count of tickets
--
-- Example:
--   SELECT get_snapshot_ticket_count('snapshot-uuid'::uuid);
-- ============================================================================

CREATE OR REPLACE FUNCTION get_snapshot_ticket_count(
    p_snapshot_id UUID
)
RETURNS BIGINT
LANGUAGE SQL
STABLE
PARALLEL SAFE
AS $$
    SELECT COUNT(*)
    FROM snapshot_ticket st
    WHERE st.snapshot_id = p_snapshot_id;
$$;

COMMENT ON FUNCTION get_snapshot_ticket_count(UUID) IS 
    'Returns total ticket count for a snapshot. Use for progress/pagination.';

-- ============================================================================
-- Function: get_tickets_for_snapshots
-- 
-- Returns tickets from multiple snapshots (useful for comparing snapshots).
--
-- Parameters:
--   p_snapshot_ids - Array of snapshot UUIDs
--
-- Returns:
--   Table of (snapshot_id, ticket_key) pairs
--
-- Example:
--   SELECT * FROM get_tickets_for_snapshots(
--       ARRAY['snapshot-uuid-1'::uuid, 'snapshot-uuid-2'::uuid]
--   );
-- ============================================================================

CREATE OR REPLACE FUNCTION get_tickets_for_snapshots(
    p_snapshot_ids UUID[]
)
RETURNS TABLE (
    snapshot_id UUID,
    ticket_key  VARCHAR(255)
)
LANGUAGE SQL
STABLE
PARALLEL SAFE
AS $$
    SELECT st.snapshot_id, st.ticket_key
    FROM snapshot_ticket st
    WHERE st.snapshot_id = ANY(p_snapshot_ids)
    ORDER BY st.snapshot_id, st.ticket_key;
$$;

COMMENT ON FUNCTION get_tickets_for_snapshots(UUID[]) IS 
    'Returns tickets from multiple snapshots for comparison operations.';

-- ============================================================================
-- End of get_tickets_for_snapshot stored procedure
-- ============================================================================
