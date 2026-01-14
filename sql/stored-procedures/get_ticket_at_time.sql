-- ============================================================================
-- Stored Procedure: get_ticket_at_time
-- Purpose: Reconstruct a single ticket's state at a specific point in time
-- PostgreSQL 15+
-- ============================================================================

-- ============================================================================
-- Function: get_ticket_at_time
-- 
-- Reconstructs the complete state of a ticket as of a given timestamp.
-- Uses window functions to find the latest value for each field before
-- the specified time.
--
-- Parameters:
--   p_company_id  - UUID of the company (required for tenant isolation)
--   p_ticket_key  - Business identifier of the ticket (e.g., 'INC0001234')
--   p_as_of_time  - Point in time to reconstruct (timestamptz)
--
-- Returns:
--   Table of (field_name, field_value, observed_at) representing the
--   ticket's state at the specified time.
--
-- Example:
--   SELECT * FROM get_ticket_at_time(
--       'company-uuid'::uuid,
--       'INC0001234',
--       '2024-01-15 09:00:00+00'::timestamptz
--   );
-- ============================================================================

CREATE OR REPLACE FUNCTION get_ticket_at_time(
    p_company_id UUID,
    p_ticket_key VARCHAR(255),
    p_as_of_time TIMESTAMPTZ
)
RETURNS TABLE (
    field_name   VARCHAR(255),
    field_value  TEXT,
    observed_at  TIMESTAMPTZ
)
LANGUAGE SQL
STABLE
PARALLEL SAFE
AS $$
    -- Use DISTINCT ON to get the latest value for each field
    -- before the as_of_time. PostgreSQL optimizes this well with
    -- the covering index on (company_id, ticket_key, field_name, observed_at DESC)
    SELECT DISTINCT ON (fc.field_name)
        fc.field_name,
        fc.field_value,
        fc.observed_at
    FROM field_change fc
    WHERE fc.company_id = p_company_id
      AND fc.ticket_key = p_ticket_key
      AND fc.observed_at <= p_as_of_time
    ORDER BY fc.field_name, fc.observed_at DESC;
$$;

COMMENT ON FUNCTION get_ticket_at_time(UUID, VARCHAR, TIMESTAMPTZ) IS 
    'Reconstructs a ticket''s complete state at a specific point in time. '
    'Returns the latest value for each field observed before or at the given time.';

-- ============================================================================
-- Function: get_ticket_at_time_jsonb
-- 
-- Alternative version that returns the ticket state as a single JSONB object.
-- Useful for applications that prefer working with JSON documents.
--
-- Parameters:
--   p_company_id  - UUID of the company (required for tenant isolation)
--   p_ticket_key  - Business identifier of the ticket
--   p_as_of_time  - Point in time to reconstruct
--
-- Returns:
--   JSONB object with field names as keys and field values as values.
--   Example: {"Status": "Open", "Priority": "High", "Assignee": "John Doe"}
--
-- Example:
--   SELECT get_ticket_at_time_jsonb(
--       'company-uuid'::uuid,
--       'INC0001234',
--       '2024-01-15 09:00:00+00'::timestamptz
--   );
-- ============================================================================

CREATE OR REPLACE FUNCTION get_ticket_at_time_jsonb(
    p_company_id UUID,
    p_ticket_key VARCHAR(255),
    p_as_of_time TIMESTAMPTZ
)
RETURNS JSONB
LANGUAGE SQL
STABLE
PARALLEL SAFE
AS $$
    SELECT COALESCE(
        jsonb_object_agg(t.field_name, t.field_value),
        '{}'::jsonb
    )
    FROM get_ticket_at_time(p_company_id, p_ticket_key, p_as_of_time) t;
$$;

COMMENT ON FUNCTION get_ticket_at_time_jsonb(UUID, VARCHAR, TIMESTAMPTZ) IS 
    'Returns a ticket''s state as a JSONB object. '
    'Wrapper around get_ticket_at_time for JSON-friendly applications.';

-- ============================================================================
-- Function: get_ticket_at_time_with_metadata
-- 
-- Extended version that includes metadata about each field's observation.
-- Useful for auditing and debugging.
--
-- Parameters:
--   p_company_id  - UUID of the company (required for tenant isolation)
--   p_ticket_key  - Business identifier of the ticket
--   p_as_of_time  - Point in time to reconstruct
--
-- Returns:
--   Table with field_name, field_value, observed_at, and snapshot_id
--   to trace the source of each value.
-- ============================================================================

CREATE OR REPLACE FUNCTION get_ticket_at_time_with_metadata(
    p_company_id UUID,
    p_ticket_key VARCHAR(255),
    p_as_of_time TIMESTAMPTZ
)
RETURNS TABLE (
    field_name   VARCHAR(255),
    field_value  TEXT,
    observed_at  TIMESTAMPTZ,
    snapshot_id  UUID
)
LANGUAGE SQL
STABLE
PARALLEL SAFE
AS $$
    SELECT DISTINCT ON (fc.field_name)
        fc.field_name,
        fc.field_value,
        fc.observed_at,
        fc.snapshot_id
    FROM field_change fc
    WHERE fc.company_id = p_company_id
      AND fc.ticket_key = p_ticket_key
      AND fc.observed_at <= p_as_of_time
    ORDER BY fc.field_name, fc.observed_at DESC;
$$;

COMMENT ON FUNCTION get_ticket_at_time_with_metadata(UUID, VARCHAR, TIMESTAMPTZ) IS 
    'Extended reconstruction with source snapshot metadata for auditing.';

-- ============================================================================
-- End of get_ticket_at_time stored procedure
-- ============================================================================
