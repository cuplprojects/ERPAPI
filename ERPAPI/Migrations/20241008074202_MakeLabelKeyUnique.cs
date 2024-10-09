using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERPAPI.Migrations
{
    /// <inheritdoc />
    public partial class MakeLabelKeyUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Key",
                table: "TextLabel");

            migrationBuilder.AddColumn<string>(
                name: "LabelKey",
                table: "TextLabel",
                type: "varchar(255)",
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_TextLabel_LabelKey",
                table: "TextLabel",
                column: "LabelKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TextLabel_LabelKey",
                table: "TextLabel");

            migrationBuilder.DropColumn(
                name: "LabelKey",
                table: "TextLabel");

            migrationBuilder.AddColumn<string>(
                name: "Key",
                table: "TextLabel",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");
        }
    }
}
