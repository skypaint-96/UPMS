namespace UPMS.Data.Migrations;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

public partial class BackgroundJobQueue : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "background_job",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                job_type = table.Column<string>(type: "varchar(100)", nullable: false),
                status = table.Column<string>(type: "varchar(50)", nullable: false),
                payload_json = table.Column<string>(type: "text", nullable: false),
                result_json = table.Column<string>(type: "text", nullable: true),
                requested_by = table.Column<string>(type: "varchar(255)", nullable: true),
                created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                started_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                completed_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                error_message = table.Column<string>(type: "text", nullable: true),
                lease_owner = table.Column<string>(type: "varchar(255)", nullable: true),
                lease_expires_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                output_file_path = table.Column<string>(type: "text", nullable: true),
                output_file_name = table.Column<string>(type: "varchar(512)", nullable: true),
                output_content_type = table.Column<string>(type: "varchar(255)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_background_job", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "ix_background_job_status_created_at",
            table: "background_job",
            columns: new[] { "status", "created_at" });

        migrationBuilder.CreateIndex(
            name: "ix_background_job_job_type_status_created_at",
            table: "background_job",
            columns: new[] { "job_type", "status", "created_at" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "background_job");
    }
}
