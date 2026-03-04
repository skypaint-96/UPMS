-- ============================================================================
-- Stored Procedure: get_ticket_at_time
-- Purpose: Reconstruct a single ticket's state at a specific point in time
-- PostgreSQL 15+
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
    SELECT DISTINCT ON (fc.field_name)
        fc.field_name,
        fc.field_value,
        fc.observed_at
    FROM field_change fc
    WHERE fc.company_name = p_company_id::text
      AND fc.ticket_key = p_ticket_key
      AND fc.observed_at <= p_as_of_time
    ORDER BY fc.field_name, fc.observed_at DESC;
$$;

COMMENT ON FUNCTION get_ticket_at_time(UUID, VARCHAR, TIMESTAMPTZ) IS
    'Reconstructs a ticket''s complete state at a specific point in time. '
    'Returns the latest value for each field observed before or at the given time.';

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
    'Returns a ticket''s state as a JSONB object. Wrapper around get_ticket_at_time.';

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
    WHERE fc.company_name = p_company_id::text
      AND fc.ticket_key = p_ticket_key
      AND fc.observed_at <= p_as_of_time
    ORDER BY fc.field_name, fc.observed_at DESC;
$$;

COMMENT ON FUNCTION get_ticket_at_time_with_metadata(UUID, VARCHAR, TIMESTAMPTZ) IS
    'Extended reconstruction with source snapshot metadata for auditing.';

-- End
