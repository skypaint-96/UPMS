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
                p_company_name TEXT,
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
                WHERE fc.company_name = p_company_name
                  AND fc.ticket_key = p_ticket_key
                  AND fc.observed_at <= p_as_of_time
                ORDER BY fc.field_name, fc.observed_at DESC;
            $$;

            COMMENT ON FUNCTION get_ticket_at_time(TEXT, VARCHAR, TIMESTAMPTZ) IS
                'Reconstructs a ticket''s complete state at a specific point in time. '
                'Returns the latest value for each field observed before or at the given time.';
            """);

        migrationBuilder.Sql("""
            CREATE OR REPLACE FUNCTION get_ticket_at_time_with_metadata(
                p_company_name TEXT,
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
                WHERE fc.company_name = p_company_name
                  AND fc.ticket_key = p_ticket_key
                  AND fc.observed_at <= p_as_of_time
                ORDER BY fc.field_name, fc.observed_at DESC;
            $$;

            COMMENT ON FUNCTION get_ticket_at_time_with_metadata(TEXT, VARCHAR, TIMESTAMPTZ) IS
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

        // -- Stored procedure: batch_reconstruct_tickets.sql
        migrationBuilder.Sql("""
            CREATE OR REPLACE FUNCTION batch_reconstruct_tickets(
                p_company_name TEXT,
                p_ticket_keys  TEXT[],
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
                WHERE fc.company_name = p_company_name
                  AND fc.ticket_key = ANY(p_ticket_keys)
                  AND fc.observed_at <= p_as_of_time
                ORDER BY fc.ticket_key, fc.field_name, fc.observed_at DESC;
            $$;

            COMMENT ON FUNCTION batch_reconstruct_tickets(TEXT, TEXT[], TIMESTAMPTZ) IS
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

        // (the batch_reconstruct_tickets function is defined once above)
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP FUNCTION IF EXISTS get_ticket_at_time(TEXT, VARCHAR, TIMESTAMPTZ) CASCADE;");
        migrationBuilder.Sql("DROP FUNCTION IF EXISTS get_ticket_at_time_with_metadata(TEXT, VARCHAR, TIMESTAMPTZ) CASCADE;");

        migrationBuilder.Sql("DROP FUNCTION IF EXISTS get_tickets_for_snapshot(UUID) CASCADE;");
        migrationBuilder.Sql("DROP FUNCTION IF EXISTS get_tickets_for_snapshot_paged(UUID, INTEGER, INTEGER) CASCADE;");
        migrationBuilder.Sql("DROP FUNCTION IF EXISTS get_snapshot_ticket_count(UUID) CASCADE;");

        migrationBuilder.Sql("DROP FUNCTION IF EXISTS batch_reconstruct_tickets(TEXT, TEXT[], TIMESTAMPTZ) CASCADE;");
        migrationBuilder.Sql("DROP FUNCTION IF EXISTS batch_reconstruct_for_snapshot(UUID) CASCADE;");
    }
}
