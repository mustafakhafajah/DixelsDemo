using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dixels.Portal.Migrations
{
    /* Buildings, floors and spaces trade the Active/Inactive "Status" for a plain "IsBookable" tick.
     * Hand-edited: the generated version dropped Status first and added IsBookable = false, which would
     * have made every building, floor and space unbookable. Here the old value is carried over:
     * Active (0) becomes bookable, Inactive (1) becomes not bookable. */
    public partial class Replace_Status_With_IsBookable : Migration
    {
        private static readonly string[] Tables = { "AppBuildings", "AppFloors", "AppSpaces" };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppSpaces_BuildingId_FloorId_Status",
                table: "AppSpaces");

            foreach (var table in Tables)
            {
                migrationBuilder.AddColumn<bool>(
                    name: "IsBookable",
                    table: table,
                    type: "boolean",
                    nullable: false,
                    defaultValue: true);

                migrationBuilder.Sql($@"UPDATE ""{table}"" SET ""IsBookable"" = (""Status"" = 0);");

                migrationBuilder.DropColumn(
                    name: "Status",
                    table: table);
            }

            migrationBuilder.CreateIndex(
                name: "IX_AppSpaces_BuildingId_FloorId",
                table: "AppSpaces",
                columns: new[] { "BuildingId", "FloorId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppSpaces_BuildingId_FloorId",
                table: "AppSpaces");

            foreach (var table in Tables)
            {
                migrationBuilder.AddColumn<int>(
                    name: "Status",
                    table: table,
                    type: "integer",
                    nullable: false,
                    defaultValue: 0);

                migrationBuilder.Sql($@"UPDATE ""{table}"" SET ""Status"" = CASE WHEN ""IsBookable"" THEN 0 ELSE 1 END;");

                migrationBuilder.DropColumn(
                    name: "IsBookable",
                    table: table);
            }

            migrationBuilder.CreateIndex(
                name: "IX_AppSpaces_BuildingId_FloorId_Status",
                table: "AppSpaces",
                columns: new[] { "BuildingId", "FloorId", "Status" });
        }
    }
}
