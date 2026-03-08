using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UPMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class _20260307000005_Add_CanonicalFieldDefinition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "canonical_field_definition",
                columns: table => new
                {
                    name = table.Column<string>(type: "varchar(255)", nullable: false),
                    data_type = table.Column<string>(type: "varchar(50)", nullable: false),
                    is_system_required = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_canonical_field_definition", x => x.name);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "canonical_field_definition");
        }
    }
}
