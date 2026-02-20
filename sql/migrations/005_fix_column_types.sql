-- ============================================================================
-- Migration: 005_fix_column_types.sql
-- Purpose: Change raw_snapshot.upload_metadata from jsonb to text so that
--          Dapper/Npgsql can pass a C# string without an explicit type cast.
--          PostgreSQL refuses an implicit text → jsonb cast; using text avoids
--          the need for NpgsqlDbType.Jsonb parameter annotations everywhere.
--
-- The USING clause makes this safe even if the column already contains data:
-- the existing jsonb value is cast to text (its JSON string representation).
-- Re-running this migration after the column is already text is a no-op because
-- ALTER COLUMN … TYPE text on a text column succeeds without modifying data.
-- ============================================================================

ALTER TABLE raw_snapshot
    ALTER COLUMN upload_metadata TYPE text
    USING upload_metadata::text;
