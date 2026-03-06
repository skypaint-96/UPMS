-- ============================================================================
-- Stored Procedure: batch_reconstruct_tickets
-- Purpose: Batch reconstruction of multiple tickets for reporting
-- PostgreSQL 15+
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
    SELECT DISTINCT ON (fc.ticket_key, fc.field_name)
        fc.ticket_key,
        fc.field_name,
        fc.field_value
    FROM field_change fc
    WHERE fc.company_name = p_company_id::text
      AND fc.ticket_key = ANY(p_ticket_keys)
      AND fc.observed_at <= p_as_of_time
    ORDER BY fc.ticket_key, fc.field_name, fc.observed_at DESC;
$$;

COMMENT ON FUNCTION batch_reconstruct_tickets(UUID, VARCHAR[], TIMESTAMPTZ) IS
    'Batch reconstruct multiple tickets at a point in time.';

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
    WITH snapshot_info AS (
        SELECT rs.snapshot_date
        FROM raw_snapshot rs
        WHERE rs.id = p_snapshot_id
    ),
    snapshot_tickets AS (
        SELECT st.ticket_key, st.company_name
        FROM snapshot_ticket st
        WHERE st.snapshot_id = p_snapshot_id
    )
    SELECT DISTINCT ON (fc.ticket_key, fc.field_name)
        fc.ticket_key,
        fc.field_name,
        fc.field_value
    FROM field_change fc
    JOIN snapshot_tickets st ON fc.ticket_key = st.ticket_key
        AND fc.company_name = st.company_name
    CROSS JOIN snapshot_info si
    WHERE fc.observed_at <= si.snapshot_date
    ORDER BY fc.ticket_key, fc.field_name, fc.observed_at DESC;
$$;

COMMENT ON FUNCTION batch_reconstruct_for_snapshot(UUID) IS
    'Reconstruct all tickets in a snapshot at the snapshot timestamp.';

-- End
