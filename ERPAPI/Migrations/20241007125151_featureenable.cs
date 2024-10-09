using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERPAPI.Migrations
{
    /// <inheritdoc />
    public partial class featureenable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Enabled",
                table: "FeatureEnabling",
                newName: "IsEnabled");

            migrationBuilder.RenameColumn(
                name: "FeatureEnablingId",
                table: "FeatureEnabling",
                newName: "ModuleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IsEnabled",
                table: "FeatureEnabling",
                newName: "Enabled");

            migrationBuilder.RenameColumn(
                name: "ModuleId",
                table: "FeatureEnabling",
                newName: "FeatureEnablingId");
        }
    }
}
