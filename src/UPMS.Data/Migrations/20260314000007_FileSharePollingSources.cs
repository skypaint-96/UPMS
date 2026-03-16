namespace UPMS.Data.Migrations;

using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

public partial class FileSharePollingSources : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "file_share_polling_source",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "varchar(200)", nullable: false),
                enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                watched_path = table.Column<string>(type: "text", nullable: false),
                file_patterns_json = table.Column<string>(type: "text", nullable: false),
                archive_path = table.Column<string>(type: "text", nullable: false),
                error_path = table.Column<string>(type: "text", nullable: false),
                itsm_source = table.Column<string>(type: "varchar(100)", nullable: false),
                poll_interval_seconds = table.Column<int>(type: "integer", nullable: false, defaultValue: 300),
                max_files_per_cycle = table.Column<int>(type: "integer", nullable: true),
                stable_file_age_seconds = table.Column<int>(type: "integer", nullable: false, defaultValue: 30),
                last_run_started_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                last_run_completed_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                last_succeeded_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                next_poll_due_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                last_error = table.Column<string>(type: "text", nullable: true),
                current_job_id = table.Column<Guid>(type: "uuid", nullable: true),
                last_job_id = table.Column<Guid>(type: "uuid", nullable: true),
                is_system_managed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                created_by = table.Column<string>(type: "varchar(255)", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                updated_by = table.Column<string>(type: "varchar(255)", nullable: true),
                updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_file_share_polling_source", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "ix_file_share_polling_source_current_job_id",
            table: "file_share_polling_source",
            column: "current_job_id");

        migrationBuilder.CreateIndex(
            name: "ix_file_share_polling_source_enabled_due_at",
            table: "file_share_polling_source",
            columns: new[] { "enabled", "next_poll_due_at" });

        migrationBuilder.CreateIndex(
            name: "ix_file_share_polling_source_name",
            table: "file_share_polling_source",
            column: "name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_file_share_polling_source_watched_path",
            table: "file_share_polling_source",
            column: "watched_path",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "file_share_polling_source");
    }
}
