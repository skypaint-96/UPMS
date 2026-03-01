namespace UPMS.Data.Migrations;

using Microsoft.EntityFrameworkCore.Migrations;

/// <inheritdoc />
public partial class StoredProcedures : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // -- Stored procedure: get_ticket_at_time.sql
        migrationBuilder.Sql("""
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
            """);

        migrationBuilder.Sql("""
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
            """);

        migrationBuilder.Sql("""
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
            """);

        // -- Stored procedure: get_tickets_for_snapshot.sql
        migrationBuilder.Sql("""
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
            """);

        migrationBuilder.Sql("""
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
            """);

        migrationBuilder.Sql("""
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
            """);

        migrationBuilder.Sql("""
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
            """);

        migrationBuilder.Sql("""
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
            """);

        // -- Stored procedure: batch_reconstruct_tickets.sql
        migrationBuilder.Sql("""
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
                'Batch reconstruct multiple tickets at a point in time. '
                'More efficient than individual calls for reporting.';
            """);

        migrationBuilder.Sql("""
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
            """);

        migrationBuilder.Sql("""
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
                WITH snapshot_tickets AS (
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
                WHERE fc.observed_at <= p_as_of_time
                ORDER BY fc.ticket_key, fc.field_name, fc.observed_at DESC;
            $$;

            COMMENT ON FUNCTION batch_reconstruct_for_snapshot_at_time(UUID, TIMESTAMPTZ) IS
                'Reconstruct snapshot tickets at a custom timestamp.';
            """);

        migrationBuilder.Sql("""
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
            """);

        migrationBuilder.Sql("""
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
                    SELECT rs.snapshot_date
                    FROM raw_snapshot rs
                    WHERE rs.id = p_snapshot_id
                ),
                paged_tickets AS (
                    SELECT st.ticket_key, st.company_name
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
                JOIN paged_tickets pt ON fc.ticket_key = pt.ticket_key
                    AND fc.company_name = pt.company_name
                CROSS JOIN snapshot_info si
                WHERE fc.observed_at <= si.snapshot_date
                ORDER BY fc.ticket_key, fc.field_name, fc.observed_at DESC;
            $$;

            COMMENT ON FUNCTION batch_reconstruct_for_snapshot_paged(UUID, INTEGER, INTEGER) IS
                'Paginated snapshot reconstruction - processes tickets in batches.';
            """);

        migrationBuilder.Sql("""
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
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // get_ticket_at_time.sql functions
        migrationBuilder.Sql("DROP FUNCTION IF EXISTS get_ticket_at_time(UUID, VARCHAR, TIMESTAMPTZ) CASCADE;");
        migrationBuilder.Sql("DROP FUNCTION IF EXISTS get_ticket_at_time_jsonb(UUID, VARCHAR, TIMESTAMPTZ) CASCADE;");
        migrationBuilder.Sql("DROP FUNCTION IF EXISTS get_ticket_at_time_with_metadata(UUID, VARCHAR, TIMESTAMPTZ) CASCADE;");

        // get_tickets_for_snapshot.sql functions
        migrationBuilder.Sql("DROP FUNCTION IF EXISTS get_tickets_for_snapshot(UUID) CASCADE;");
        migrationBuilder.Sql("DROP FUNCTION IF EXISTS get_tickets_for_snapshot_paged(UUID, INTEGER, INTEGER) CASCADE;");
        migrationBuilder.Sql("DROP FUNCTION IF EXISTS get_tickets_for_snapshot_cursor(UUID, INTEGER, VARCHAR) CASCADE;");
        migrationBuilder.Sql("DROP FUNCTION IF EXISTS get_snapshot_ticket_count(UUID) CASCADE;");
        migrationBuilder.Sql("DROP FUNCTION IF EXISTS get_tickets_for_snapshots(UUID[]) CASCADE;");

        // batch_reconstruct_tickets.sql functions
        migrationBuilder.Sql("DROP FUNCTION IF EXISTS batch_reconstruct_tickets(UUID, VARCHAR[], TIMESTAMPTZ) CASCADE;");
        migrationBuilder.Sql("DROP FUNCTION IF EXISTS batch_reconstruct_for_snapshot(UUID) CASCADE;");
        migrationBuilder.Sql("DROP FUNCTION IF EXISTS batch_reconstruct_for_snapshot_at_time(UUID, TIMESTAMPTZ) CASCADE;");
        migrationBuilder.Sql("DROP FUNCTION IF EXISTS batch_reconstruct_paged(UUID, VARCHAR[], TIMESTAMPTZ, INTEGER, INTEGER) CASCADE;");
        migrationBuilder.Sql("DROP FUNCTION IF EXISTS batch_reconstruct_for_snapshot_paged(UUID, INTEGER, INTEGER) CASCADE;");
        migrationBuilder.Sql("DROP FUNCTION IF EXISTS batch_reconstruct_as_jsonb(UUID, VARCHAR[], TIMESTAMPTZ) CASCADE;");
    }
}
