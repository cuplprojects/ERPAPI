using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERPAPI.Migrations
{
    /// <inheritdoc />
    public partial class nuget : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GroupId",
                table: "ProjectProcesses");

            migrationBuilder.DropColumn(
                name: "TypeId",
                table: "ProjectProcesses");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GroupId",
                table: "ProjectProcesses",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TypeId",
                table: "ProjectProcesses",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
