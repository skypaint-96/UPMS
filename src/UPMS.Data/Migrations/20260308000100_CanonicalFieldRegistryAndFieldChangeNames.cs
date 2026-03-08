using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UPMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class CanonicalFieldRegistryAndFieldChangeNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS canonical_field_definition
                (
                    name VARCHAR(255) PRIMARY KEY,
                    data_type VARCHAR(50) NOT NULL,
                    is_system_required BOOLEAN NOT NULL DEFAULT FALSE
                );
                """);

            migrationBuilder.Sql("""
                ALTER TABLE field_change
                ADD COLUMN IF NOT EXISTS canonical_field_name VARCHAR(255);
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_upms_field_change_no_update ON field_change;
                """);

            migrationBuilder.Sql("""
                UPDATE field_change
                SET canonical_field_name = field_name
                WHERE canonical_field_name IS NULL OR btrim(canonical_field_name) = '';
                """);

            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION upms_field_change_prevent_update()
                RETURNS trigger AS $$
                BEGIN
                    RAISE EXCEPTION 'field_change is append-only; updates are not allowed.';
                END;
                $$ LANGUAGE plpgsql;

                DROP TRIGGER IF EXISTS trg_upms_field_change_no_update ON field_change;
                CREATE TRIGGER trg_upms_field_change_no_update
                BEFORE UPDATE ON field_change
                FOR EACH ROW
                EXECUTE FUNCTION upms_field_change_prevent_update();
                """);

            migrationBuilder.Sql("""
                INSERT INTO canonical_field_definition (name, data_type, is_system_required)
                VALUES
                ('Active', 'Boolean', FALSE),
                ('Assigned To', 'Text', FALSE),
                ('Assignment Group', 'Text', FALSE),
                ('Business Service', 'Text', FALSE),
                ('Category', 'Text', FALSE),
                ('Cause Notes', 'Text', FALSE),
                ('Closed At', 'DateTime', FALSE),
                ('Closed By', 'Text', FALSE),
                ('Close Notes', 'Text', FALSE),
                ('Cmdb Ci', 'Text', FALSE),
                ('Comments', 'Text', FALSE),
                ('Comments And Work Notes', 'Text', FALSE),
                ('Company', 'Text', TRUE),
                ('Confirmed At', 'DateTime', FALSE),
                ('Confirmed By', 'Text', FALSE),
                ('Correlation Display', 'Text', FALSE),
                ('Correlation Id', 'Text', FALSE),
                ('Description', 'Text', FALSE),
                ('First Reported By Task', 'Text', FALSE),
                ('Fix Notes', 'Text', FALSE),
                ('Knowledge', 'Text', FALSE),
                ('Major Problem', 'Text', FALSE),
                ('Number', 'Text', TRUE),
                ('Opened At', 'DateTime', FALSE),
                ('Opened By', 'Text', FALSE),
                ('Priority', 'Text', FALSE),
                ('Related Incidents', 'Text', FALSE),
                ('Resolution Code', 'Text', FALSE),
                ('Resolved At', 'DateTime', FALSE),
                ('Resolved By', 'Text', FALSE),
                ('Service Offering', 'Text', FALSE),
                ('Short Description', 'Text', FALSE),
                ('State', 'Text', FALSE),
                ('Subcategory', 'Text', FALSE),
                ('Created By', 'Text', FALSE),
                ('Created On', 'DateTime', FALSE),
                ('Updated By', 'Text', FALSE),
                ('Updated On', 'DateTime', FALSE),
                ('Investigation Driver', 'Text', FALSE),
                ('Root Cause Code', 'Text', FALSE),
                ('Root Cause Date', 'DateTime', FALSE),
                ('Workaround', 'Text', FALSE)
                ON CONFLICT (name) DO UPDATE
                SET is_system_required = canonical_field_definition.is_system_required OR EXCLUDED.is_system_required;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE field_change
                DROP COLUMN IF EXISTS canonical_field_name;
                """);

            migrationBuilder.Sql("""
                DROP TABLE IF EXISTS canonical_field_definition;
                """);
        }
    }
}
