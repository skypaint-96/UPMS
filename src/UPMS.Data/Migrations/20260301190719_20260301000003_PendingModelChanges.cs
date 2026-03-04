using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace UPMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class _20260301000003_PendingModelChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_field_change_company_name_ticket_key",
                table: "field_change");

            migrationBuilder.DropIndex(
                name: "IX_field_change_observed_at",
                table: "field_change");

            // Removed RenameTable operations on keyless DTOs; keep only real-table changes

            migrationBuilder.AlterColumn<int>(
                name: "id",
                table: "itsm_source",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn);

            migrationBuilder.AlterColumn<long>(
                name: "id",
                table: "field_change",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert id column generation strategy changes
            migrationBuilder.AlterColumn<int>(
                name: "id",
                table: "itsm_source",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn)
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);
 
            migrationBuilder.AlterColumn<long>(
                name: "id",
                table: "field_change",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn)
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);
 
            // Recreate dropped indexes
            migrationBuilder.CreateIndex(
                name: "IX_field_change_company_name_ticket_key",
                table: "field_change",
                columns: new[] { "company_name", "ticket_key" });
 
            migrationBuilder.CreateIndex(
                name: "IX_field_change_observed_at",
                table: "field_change",
                column: "observed_at");
        }
    }
}
