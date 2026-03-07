using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UPMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class _20260306000004_FieldChangeAppendOnly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Make field_change append-only at the database level.
            // If anything attempts to UPDATE an existing row, fail fast.
            // This protects change history integrity.
            migrationBuilder.Sql(@"
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
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP TRIGGER IF EXISTS trg_upms_field_change_no_update ON field_change;
DROP FUNCTION IF EXISTS upms_field_change_prevent_update();
");
        }
    }
}
