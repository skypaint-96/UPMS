-- ============================================================================
-- Stored Procedure: batch_reconstruct_tickets
-- Purpose: Batch reconstruction of multiple tickets for reporting
-- PostgreSQL 15+
-- ============================================================================

-- ============================================================================
-- Function: batch_reconstruct_tickets
-- 
-- Reconstructs the state of multiple tickets at a specific point in time.
-- Optimized for batch operations - more efficient than calling 
-- get_ticket_at_time in a loop.
--
-- Parameters:
--   p_company_id   - UUID of the company (required for tenant isolation)
--   p_ticket_keys  - Array of ticket keys to reconstruct
--   p_as_of_time   - Point in time to reconstruct
--
-- Returns:
--   Table of (ticket_key, field_name, field_value) representing the
--   state of all requested tickets at the specified time.
--
-- Example:
--   SELECT * FROM batch_reconstruct_tickets(
--       'company-uuid'::uuid,
--       ARRAY['INC0001234', 'INC0001235', 'INC0001236'],
--       '2024-01-15 09:00:00+00'::timestamptz
--   );
-- ============================================================================

CREATE OR REPLACE FUNCTION batch_reconstruct_tickets(
    p_company_id   UUID,
    p_ticket_keys  VARCHAR(255)[],
    p_as_of_time   TIMESTAMPTZ
)
RETURNS TABLE (
    ticket_key   VARCHAR(255),
    field_name   VARCHAR(255),
    field_value  TEXT
)
LANGUAGE SQL
STABLE
PARALLEL SAFE
AS $$
    -- Use DISTINCT ON to get the latest value for each (ticket_key, field_name)
    -- combination before the as_of_time.
    -- The covering index supports this query pattern efficiently.
    SELECT DISTINCT ON (fc.ticket_key, fc.field_name)
        fc.ticket_key,
        fc.field_name,
        fc.field_value
    FROM field_change fc
    WHERE fc.company_id = p_company_id
      AND fc.ticket_key = ANY(p_ticket_keys)
      AND fc.observed_at <= p_as_of_time
    ORDER BY fc.ticket_key, fc.field_name, fc.observed_at DESC;
$$;

COMMENT ON FUNCTION batch_reconstruct_tickets(UUID, VARCHAR[], TIMESTAMPTZ) IS 
    'Batch reconstruct multiple tickets at a point in time. '
    'More efficient than individual calls for reporting.';

-- ============================================================================
-- Function: batch_reconstruct_for_snapshot
-- 
-- Reconstructs all tickets from a snapshot at the snapshot's timestamp.
-- Combines snapshot_ticket lookup with batch reconstruction.
--
-- Parameters:
--   p_snapshot_id - UUID of the snapshot to reconstruct
--
-- Returns:
--   Table of (ticket_key, field_name, field_value) for all tickets
--   in the snapshot.
--
-- Example:
--   SELECT * FROM batch_reconstruct_for_snapshot('snapshot-uuid'::uuid);
-- ============================================================================

CREATE OR REPLACE FUNCTION batch_reconstruct_for_snapshot(
    p_snapshot_id UUID
)
RETURNS TABLE (
    ticket_key   VARCHAR(255),
    field_name   VARCHAR(255),
    field_value  TEXT
)
LANGUAGE SQL
STABLE
PARALLEL SAFE
AS $$
    -- First get the snapshot metadata, then reconstruct all tickets
    WITH snapshot_info AS (
        SELECT rs.company_id, rs.snapshot_date
        FROM raw_snapshot rs
        WHERE rs.id = p_snapshot_id
    ),
    snapshot_tickets AS (
        SELECT st.ticket_key
        FROM snapshot_ticket st
        WHERE st.snapshot_id = p_snapshot_id
    )
    SELECT DISTINCT ON (fc.ticket_key, fc.field_name)
        fc.ticket_key,
        fc.field_name,
        fc.field_value
    FROM field_change fc
    CROSS JOIN snapshot_info si
    WHERE fc.company_id = si.company_id
      AND fc.ticket_key IN (SELECT ticket_key FROM snapshot_tickets)
      AND fc.observed_at <= si.snapshot_date
    ORDER BY fc.ticket_key, fc.field_name, fc.observed_at DESC;
$$;

COMMENT ON FUNCTION batch_reconstruct_for_snapshot(UUID) IS 
    'Reconstruct all tickets in a snapshot at the snapshot timestamp.';

-- ============================================================================
-- Function: batch_reconstruct_for_snapshot_at_time
-- 
-- Reconstructs all tickets from a snapshot at a custom point in time.
-- Useful for "what did this snapshot look like at a different time".
--
-- Parameters:
--   p_snapshot_id - UUID of the snapshot (defines which tickets)
--   p_as_of_time  - Custom point in time for reconstruction
--
-- Returns:
--   Table of (ticket_key, field_name, field_value)
--
-- Example:
--   SELECT * FROM batch_reconstruct_for_snapshot_at_time(
--       'snapshot-uuid'::uuid,
--       '2024-01-20 09:00:00+00'::timestamptz
--   );
-- ============================================================================

CREATE OR REPLACE FUNCTION batch_reconstruct_for_snapshot_at_time(
    p_snapshot_id UUID,
    p_as_of_time  TIMESTAMPTZ
)
RETURNS TABLE (
    ticket_key   VARCHAR(255),
    field_name   VARCHAR(255),
    field_value  TEXT
)
LANGUAGE SQL
STABLE
PARALLEL SAFE
AS $$
    WITH snapshot_info AS (
        SELECT rs.company_id
        FROM raw_snapshot rs
        WHERE rs.id = p_snapshot_id
    ),
    snapshot_tickets AS (
        SELECT st.ticket_key
        FROM snapshot_ticket st
        WHERE st.snapshot_id = p_snapshot_id
    )
    SELECT DISTINCT ON (fc.ticket_key, fc.field_name)
        fc.ticket_key,
        fc.field_name,
        fc.field_value
    FROM field_change fc
    CROSS JOIN snapshot_info si
    WHERE fc.company_id = si.company_id
      AND fc.ticket_key IN (SELECT ticket_key FROM snapshot_tickets)
      AND fc.observed_at <= p_as_of_time
    ORDER BY fc.ticket_key, fc.field_name, fc.observed_at DESC;
$$;

COMMENT ON FUNCTION batch_reconstruct_for_snapshot_at_time(UUID, TIMESTAMPTZ) IS 
    'Reconstruct snapshot tickets at a custom timestamp.';

-- ============================================================================
-- Function: batch_reconstruct_paged
-- 
-- Paginated batch reconstruction for large result sets.
-- Supports streaming by returning results in chunks.
--
-- Parameters:
--   p_company_id   - UUID of the company
--   p_ticket_keys  - Array of ticket keys to reconstruct
--   p_as_of_time   - Point in time to reconstruct
--   p_page_size    - Maximum rows per page
--   p_offset       - Number of rows to skip
--
-- Returns:
--   Table of (ticket_key, field_name, field_value)
--
-- Note: For true streaming, consider using cursors in your application layer.
--
-- Example:
--   SELECT * FROM batch_reconstruct_paged(
--       'company-uuid'::uuid,
--       ARRAY['INC0001234', 'INC0001235'],
--       '2024-01-15 09:00:00+00'::timestamptz,
--       1000,
--       0
--   );
-- ============================================================================

CREATE OR REPLACE FUNCTION batch_reconstruct_paged(
    p_company_id   UUID,
    p_ticket_keys  VARCHAR(255)[],
    p_as_of_time   TIMESTAMPTZ,
    p_page_size    INTEGER DEFAULT 10000,
    p_offset       INTEGER DEFAULT 0
)
RETURNS TABLE (
    ticket_key   VARCHAR(255),
    field_name   VARCHAR(255),
    field_value  TEXT
)
LANGUAGE SQL
STABLE
PARALLEL SAFE
AS $$
    SELECT 
        r.ticket_key,
        r.field_name,
        r.field_value
    FROM batch_reconstruct_tickets(p_company_id, p_ticket_keys, p_as_of_time) r
    ORDER BY r.ticket_key, r.field_name
    LIMIT p_page_size
    OFFSET p_offset;
$$;

COMMENT ON FUNCTION batch_reconstruct_paged(UUID, VARCHAR[], TIMESTAMPTZ, INTEGER, INTEGER) IS 
    'Paginated batch reconstruction for streaming large result sets.';

-- ============================================================================
-- Function: batch_reconstruct_for_snapshot_paged
-- 
-- Paginated version of snapshot reconstruction.
-- Reconstructs tickets in batches for memory-efficient processing.
--
-- Parameters:
--   p_snapshot_id  - UUID of the snapshot
--   p_page_size    - Number of tickets to reconstruct per call
--   p_ticket_offset - Ticket offset (not row offset)
--
-- Returns:
--   Table of (ticket_key, field_name, field_value)
--
-- Example:
--   -- First batch of 100 tickets
--   SELECT * FROM batch_reconstruct_for_snapshot_paged('snapshot-uuid'::uuid, 100, 0);
--   -- Next batch
--   SELECT * FROM batch_reconstruct_for_snapshot_paged('snapshot-uuid'::uuid, 100, 100);
-- ============================================================================

CREATE OR REPLACE FUNCTION batch_reconstruct_for_snapshot_paged(
    p_snapshot_id    UUID,
    p_page_size      INTEGER DEFAULT 100,
    p_ticket_offset  INTEGER DEFAULT 0
)
RETURNS TABLE (
    ticket_key   VARCHAR(255),
    field_name   VARCHAR(255),
    field_value  TEXT
)
LANGUAGE SQL
STABLE
PARALLEL SAFE
AS $$
    WITH snapshot_info AS (
        SELECT rs.company_id, rs.snapshot_date
        FROM raw_snapshot rs
        WHERE rs.id = p_snapshot_id
    ),
    paged_tickets AS (
        SELECT st.ticket_key
        FROM snapshot_ticket st
        WHERE st.snapshot_id = p_snapshot_id
        ORDER BY st.ticket_key
        LIMIT p_page_size
        OFFSET p_ticket_offset
    )
    SELECT DISTINCT ON (fc.ticket_key, fc.field_name)
        fc.ticket_key,
        fc.field_name,
        fc.field_value
    FROM field_change fc
    CROSS JOIN snapshot_info si
    WHERE fc.company_id = si.company_id
      AND fc.ticket_key IN (SELECT ticket_key FROM paged_tickets)
      AND fc.observed_at <= si.snapshot_date
    ORDER BY fc.ticket_key, fc.field_name, fc.observed_at DESC;
$$;

COMMENT ON FUNCTION batch_reconstruct_for_snapshot_paged(UUID, INTEGER, INTEGER) IS 
    'Paginated snapshot reconstruction - processes tickets in batches.';

-- ============================================================================
-- Function: batch_reconstruct_as_jsonb
-- 
-- Batch reconstruction returning each ticket as a JSONB object.
-- Useful for APIs that work with JSON documents.
--
-- Parameters:
--   p_company_id   - UUID of the company
--   p_ticket_keys  - Array of ticket keys to reconstruct
--   p_as_of_time   - Point in time to reconstruct
--
-- Returns:
--   Table of (ticket_key, ticket_data) where ticket_data is JSONB
--
-- Example:
--   SELECT * FROM batch_reconstruct_as_jsonb(
--       'company-uuid'::uuid,
--       ARRAY['INC0001234', 'INC0001235'],
--       '2024-01-15 09:00:00+00'::timestamptz
--   );
-- ============================================================================

CREATE OR REPLACE FUNCTION batch_reconstruct_as_jsonb(
    p_company_id   UUID,
    p_ticket_keys  VARCHAR(255)[],
    p_as_of_time   TIMESTAMPTZ
)
RETURNS TABLE (
    ticket_key   VARCHAR(255),
    ticket_data  JSONB
)
LANGUAGE SQL
STABLE
PARALLEL SAFE
AS $$
    SELECT 
        r.ticket_key,
        jsonb_object_agg(r.field_name, r.field_value) AS ticket_data
    FROM batch_reconstruct_tickets(p_company_id, p_ticket_keys, p_as_of_time) r
    GROUP BY r.ticket_key
    ORDER BY r.ticket_key;
$$;

COMMENT ON FUNCTION batch_reconstruct_as_jsonb(UUID, VARCHAR[], TIMESTAMPTZ) IS 
    'Batch reconstruction returning each ticket as a JSONB document.';

-- ============================================================================
-- End of batch_reconstruct_tickets stored procedure
-- ============================================================================
