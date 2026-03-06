namespace UPMS.Data.Migrations;

using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

/// <inheritdoc />
public partial class InitialSchema : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Enable uuid-ossp for uuid_generate_v4() support
        migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS \"uuid-ossp\";");

        // ── raw_snapshot ───────────────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "raw_snapshot",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                itsm_source = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                snapshot_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                uploaded_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                uploaded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                upload_metadata = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_raw_snapshot", x => x.id);
            });

        // ── itsm_source ────────────────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "itsm_source",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                name = table.Column<string>(type: "text", nullable: false),
                display_label = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_itsm_source", x => x.id);
            });

        // ── itsm_field_mapping ─────────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "itsm_field_mapping",
            columns: table => new
            {
                itsm_source = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                source_field_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                canonical_field_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                is_required = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_itsm_field_mapping", x => new { x.itsm_source, x.source_field_name });
            });

        // ── snapshot_ticket ────────────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "snapshot_ticket",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                company_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                ticket_key = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_snapshot_ticket", x => x.id);
                table.UniqueConstraint("uq_snapshot_ticket", x => new { x.snapshot_id, x.ticket_key });
                table.ForeignKey(
                    name: "fk_snapshot_ticket_snapshot_id",
                    column: x => x.snapshot_id,
                    principalTable: "raw_snapshot",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        // ── field_change ───────────────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "field_change",
            columns: table => new
            {
                id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                company_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                ticket_key = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                field_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                field_value = table.Column<string>(type: "text", nullable: true),
                observed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                snapshot_id = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_field_change", x => x.id);
                table.ForeignKey(
                    name: "fk_field_change_snapshot_id",
                    column: x => x.snapshot_id,
                    principalTable: "raw_snapshot",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        // ── Indexes ────────────────────────────────────────────────────────────

        // itsm_source: unique name
        migrationBuilder.CreateIndex(
            name: "IX_itsm_source_name",
            table: "itsm_source",
            column: "name",
            unique: true);

        // itsm_field_mapping: lookup by itsm_source
        migrationBuilder.CreateIndex(
            name: "IX_itsm_field_mapping_itsm_source",
            table: "itsm_field_mapping",
            column: "itsm_source");

        // snapshot_ticket: lookup by snapshot_id
        migrationBuilder.CreateIndex(
            name: "IX_snapshot_ticket_snapshot_id",
            table: "snapshot_ticket",
            column: "snapshot_id");

        // field_change: lookup by snapshot_id
        migrationBuilder.CreateIndex(
            name: "IX_field_change_snapshot_id",
            table: "field_change",
            column: "snapshot_id");

        // field_change: tenant + ticket lookup
        migrationBuilder.CreateIndex(
            name: "IX_field_change_company_name_ticket_key",
            table: "field_change",
            columns: new[] { "company_name", "ticket_key" });

        // field_change: time-range scans
        migrationBuilder.CreateIndex(
            name: "IX_field_change_observed_at",
            table: "field_change",
            column: "observed_at");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Drop in reverse FK dependency order
        migrationBuilder.DropTable(name: "itsm_field_mapping");
        migrationBuilder.DropTable(name: "itsm_source");
        migrationBuilder.DropTable(name: "field_change");
        migrationBuilder.DropTable(name: "snapshot_ticket");
        migrationBuilder.DropTable(name: "raw_snapshot");
    }
}
