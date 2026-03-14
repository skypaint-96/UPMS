namespace UPMS.Data.Migrations;

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

[DbContext(typeof(UpmsDbContext))]
[Migration("20260313000007_AddProblemRequestManagement")]
public partial class AddProblemRequestManagement : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "problem_request",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                requester_name = table.Column<string>(type: "varchar(255)", nullable: false),
                requester_email = table.Column<string>(type: "varchar(320)", nullable: true),
                requester_team = table.Column<string>(type: "varchar(255)", nullable: true),
                company_name = table.Column<string>(type: "varchar(255)", nullable: true),
                itsm_source = table.Column<string>(type: "varchar(100)", nullable: true),
                title = table.Column<string>(type: "varchar(500)", nullable: false),
                description = table.Column<string>(type: "text", nullable: false),
                justification = table.Column<string>(type: "text", nullable: false),
                status = table.Column<string>(type: "varchar(50)", nullable: false),
                assignee = table.Column<string>(type: "varchar(255)", nullable: true),
                decision_reason = table.Column<string>(type: "text", nullable: true),
                problem_reference = table.Column<string>(type: "varchar(512)", nullable: true),
                problem_itsm_source = table.Column<string>(type: "varchar(100)", nullable: true),
                problem_company_name = table.Column<string>(type: "varchar(255)", nullable: true),
                problem_ticket_key = table.Column<string>(type: "varchar(255)", nullable: true),
                created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                linked_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_problem_request", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "problem_request_comment",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                problem_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                author_name = table.Column<string>(type: "varchar(255)", nullable: true),
                comment_text = table.Column<string>(type: "text", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_problem_request_comment", x => x.id);
                table.ForeignKey(
                    name: "fk_problem_request_comment_request_id",
                    column: x => x.problem_request_id,
                    principalTable: "problem_request",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_problem_request_assignee",
            table: "problem_request",
            column: "assignee");

        migrationBuilder.CreateIndex(
            name: "ix_problem_request_company_source",
            table: "problem_request",
            columns: new[] { "company_name", "itsm_source" });

        migrationBuilder.CreateIndex(
            name: "ix_problem_request_status_updated_at",
            table: "problem_request",
            columns: new[] { "status", "updated_at" });

        migrationBuilder.CreateIndex(
            name: "ix_problem_request_comment_request_created_at",
            table: "problem_request_comment",
            columns: new[] { "problem_request_id", "created_at" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "problem_request_comment");
        migrationBuilder.DropTable(name: "problem_request");
    }
}
