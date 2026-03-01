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

            migrationBuilder.RenameTable(
                name: "TicketFieldWithMetadataDto",
                newName: "TicketFieldWithMetadataResults");

            migrationBuilder.RenameTable(
                name: "TicketFieldAtTimeDto",
                newName: "TicketFieldAtTimeResults");

            migrationBuilder.RenameTable(
                name: "SnapshotTicketPairDto",
                newName: "SnapshotTicketPairResults");

            migrationBuilder.RenameTable(
                name: "SnapshotTicketKeyDto",
                newName: "SnapshotTicketKeyResults");

            migrationBuilder.RenameTable(
                name: "ReconstructedFieldDto",
                newName: "ReconstructedFieldResults");

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

            migrationBuilder.AlterColumn<string>(
                name: "field_name",
                table: "TicketFieldWithMetadataResults",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "field_name",
                table: "TicketFieldAtTimeResults",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ticket_key",
                table: "SnapshotTicketPairResults",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ticket_key",
                table: "SnapshotTicketKeyResults",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ticket_key",
                table: "ReconstructedFieldResults",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "field_name",
                table: "ReconstructedFieldResults",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "TicketFieldWithMetadataResults",
                newName: "TicketFieldWithMetadataDto");

            migrationBuilder.RenameTable(
                name: "TicketFieldAtTimeResults",
                newName: "TicketFieldAtTimeDto");

            migrationBuilder.RenameTable(
                name: "SnapshotTicketPairResults",
                newName: "SnapshotTicketPairDto");

            migrationBuilder.RenameTable(
                name: "SnapshotTicketKeyResults",
                newName: "SnapshotTicketKeyDto");

            migrationBuilder.RenameTable(
                name: "ReconstructedFieldResults",
                newName: "ReconstructedFieldDto");

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

            migrationBuilder.AlterColumn<string>(
                name: "field_name",
                table: "TicketFieldWithMetadataDto",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "field_name",
                table: "TicketFieldAtTimeDto",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "ticket_key",
                table: "SnapshotTicketPairDto",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "ticket_key",
                table: "SnapshotTicketKeyDto",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "ticket_key",
                table: "ReconstructedFieldDto",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "field_name",
                table: "ReconstructedFieldDto",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

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
