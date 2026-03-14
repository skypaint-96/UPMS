namespace UPMS.Data.Migrations;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

public partial class ReportDeliveryDistributionLists : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "company_profile",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                company_key = table.Column<string>(type: "varchar(255)", nullable: false),
                display_name = table.Column<string>(type: "varchar(255)", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_company_profile", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "report_delivery",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                report_job_id = table.Column<Guid>(type: "uuid", nullable: false),
                last_background_job_id = table.Column<Guid>(type: "uuid", nullable: true),
                company_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                company_key = table.Column<string>(type: "varchar(255)", nullable: false),
                company_display_name = table.Column<string>(type: "varchar(255)", nullable: false),
                distribution_list_id = table.Column<Guid>(type: "uuid", nullable: false),
                distribution_list_name = table.Column<string>(type: "varchar(255)", nullable: false),
                channel = table.Column<string>(type: "varchar(50)", nullable: false),
                status = table.Column<string>(type: "varchar(50)", nullable: false),
                artifact_path = table.Column<string>(type: "text", nullable: false),
                artifact_file_name = table.Column<string>(type: "varchar(512)", nullable: true),
                artifact_content_type = table.Column<string>(type: "varchar(255)", nullable: true),
                subject = table.Column<string>(type: "varchar(512)", nullable: false),
                recipient_snapshot_json = table.Column<string>(type: "text", nullable: false),
                recipient_count = table.Column<int>(type: "integer", nullable: false),
                attempt_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                requested_by = table.Column<string>(type: "varchar(255)", nullable: true),
                created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                started_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                completed_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                last_attempted_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                last_error_message = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_report_delivery", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "distribution_list",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                company_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "varchar(255)", nullable: false),
                description = table.Column<string>(type: "text", nullable: true),
                is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                created_by = table.Column<string>(type: "varchar(255)", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                updated_by = table.Column<string>(type: "varchar(255)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_distribution_list", x => x.id);
                table.ForeignKey(
                    name: "fk_distribution_list_company_profile_id",
                    column: x => x.company_profile_id,
                    principalTable: "company_profile",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "distribution_list_recipient",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                distribution_list_id = table.Column<Guid>(type: "uuid", nullable: false),
                channel = table.Column<string>(type: "varchar(50)", nullable: false),
                endpoint = table.Column<string>(type: "varchar(512)", nullable: false),
                display_name = table.Column<string>(type: "varchar(255)", nullable: true),
                metadata_json = table.Column<string>(type: "text", nullable: true),
                is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_distribution_list_recipient", x => x.id);
                table.ForeignKey(
                    name: "fk_distribution_list_recipient_distribution_list_id",
                    column: x => x.distribution_list_id,
                    principalTable: "distribution_list",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_company_profile_company_key",
            table: "company_profile",
            column: "company_key",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_company_profile_display_name",
            table: "company_profile",
            column: "display_name");

        migrationBuilder.CreateIndex(
            name: "ix_distribution_list_company_profile_id_is_active_name",
            table: "distribution_list",
            columns: new[] { "company_profile_id", "is_active", "name" });

        migrationBuilder.CreateIndex(
            name: "ix_distribution_list_company_profile_id_name",
            table: "distribution_list",
            columns: new[] { "company_profile_id", "name" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_distribution_list_recipient_distribution_list_id_channel_endpoint",
            table: "distribution_list_recipient",
            columns: new[] { "distribution_list_id", "channel", "endpoint" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_distribution_list_recipient_distribution_list_id_is_active_sort_order",
            table: "distribution_list_recipient",
            columns: new[] { "distribution_list_id", "is_active", "sort_order" });

        migrationBuilder.CreateIndex(
            name: "ix_report_delivery_company_key_created_at",
            table: "report_delivery",
            columns: new[] { "company_key", "created_at" });

        migrationBuilder.CreateIndex(
            name: "ix_report_delivery_distribution_list_id_created_at",
            table: "report_delivery",
            columns: new[] { "distribution_list_id", "created_at" });

        migrationBuilder.CreateIndex(
            name: "ix_report_delivery_report_job_id_created_at",
            table: "report_delivery",
            columns: new[] { "report_job_id", "created_at" });

        migrationBuilder.CreateIndex(
            name: "ix_report_delivery_status_created_at",
            table: "report_delivery",
            columns: new[] { "status", "created_at" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "distribution_list_recipient");
        migrationBuilder.DropTable(name: "report_delivery");
        migrationBuilder.DropTable(name: "distribution_list");
        migrationBuilder.DropTable(name: "company_profile");
    }
}
