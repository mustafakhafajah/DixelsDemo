using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dixels.Portal.Migrations
{
    /* Names (and a space's note) move from a column on each table to one row per language in a
     * {Table}Translations table. Every existing name becomes the English ("en") row, so nothing is lost. */
    /// <inheritdoc />
    public partial class Add_Multilingual_Estate_Names : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppBuildingTranslations",
                columns: table => new
                {
                    BuildingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppBuildingTranslations", x => new { x.BuildingId, x.Language });
                    table.ForeignKey(
                        name: "FK_AppBuildingTranslations_AppBuildings_BuildingId",
                        column: x => x.BuildingId,
                        principalTable: "AppBuildings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppFloorTranslations",
                columns: table => new
                {
                    FloorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppFloorTranslations", x => new { x.FloorId, x.Language });
                    table.ForeignKey(
                        name: "FK_AppFloorTranslations_AppFloors_FloorId",
                        column: x => x.FloorId,
                        principalTable: "AppFloors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppSpaceTranslations",
                columns: table => new
                {
                    SpaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSpaceTranslations", x => new { x.SpaceId, x.Language });
                    table.ForeignKey(
                        name: "FK_AppSpaceTranslations_AppSpaces_SpaceId",
                        column: x => x.SpaceId,
                        principalTable: "AppSpaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppSpaceTypeTranslations",
                columns: table => new
                {
                    SpaceTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSpaceTypeTranslations", x => new { x.SpaceTypeId, x.Language });
                    table.ForeignKey(
                        name: "FK_AppSpaceTypeTranslations_AppSpaceTypes_SpaceTypeId",
                        column: x => x.SpaceTypeId,
                        principalTable: "AppSpaceTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            /* Every row, soft-deleted ones included, so a restored record still has its name. */
            migrationBuilder.Sql(@"
INSERT INTO ""AppBuildingTranslations"" (""BuildingId"", ""Language"", ""Name"") SELECT ""Id"", 'en', ""Name"" FROM ""AppBuildings"";
INSERT INTO ""AppFloorTranslations"" (""FloorId"", ""Language"", ""Name"") SELECT ""Id"", 'en', ""Name"" FROM ""AppFloors"";
INSERT INTO ""AppSpaceTypeTranslations"" (""SpaceTypeId"", ""Language"", ""Name"") SELECT ""Id"", 'en', ""Name"" FROM ""AppSpaceTypes"";
INSERT INTO ""AppSpaceTranslations"" (""SpaceId"", ""Language"", ""Name"", ""Note"") SELECT ""Id"", 'en', ""Name"", ""Note"" FROM ""AppSpaces"";");

            migrationBuilder.DropIndex(
                name: "IX_AppSpaceTypes_Name",
                table: "AppSpaceTypes");

            migrationBuilder.DropIndex(
                name: "IX_AppSpaces_Name",
                table: "AppSpaces");

            migrationBuilder.DropIndex(
                name: "IX_AppFloors_BuildingId_Name",
                table: "AppFloors");

            migrationBuilder.DropIndex(
                name: "IX_AppBuildings_Name",
                table: "AppBuildings");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "AppSpaceTypes");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "AppSpaces");

            migrationBuilder.DropColumn(
                name: "Note",
                table: "AppSpaces");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "AppFloors");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "AppBuildings");

            migrationBuilder.CreateIndex(
                name: "IX_AppFloors_BuildingId",
                table: "AppFloors",
                column: "BuildingId");

            migrationBuilder.CreateIndex(
                name: "IX_AppBuildingTranslations_Language_Name",
                table: "AppBuildingTranslations",
                columns: new[] { "Language", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppSpaceTranslations_Language_Name",
                table: "AppSpaceTranslations",
                columns: new[] { "Language", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppSpaceTypeTranslations_Language_Name",
                table: "AppSpaceTypeTranslations",
                columns: new[] { "Language", "Name" },
                unique: true);
        }

        /* Back to one column: the English name, else the first language that has one. Names only typed in other
         * languages are lost, and two records whose chosen names clash make the unique indexes fail. */
        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppFloors_BuildingId",
                table: "AppFloors");

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "AppSpaceTypes",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "AppSpaces",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "AppSpaces",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "AppFloors",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "AppBuildings",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            foreach (var (table, translations, key) in new[]
                     {
                         ("AppBuildings", "AppBuildingTranslations", "BuildingId"),
                         ("AppFloors", "AppFloorTranslations", "FloorId"),
                         ("AppSpaceTypes", "AppSpaceTypeTranslations", "SpaceTypeId"),
                         ("AppSpaces", "AppSpaceTranslations", "SpaceId"),
                     })
                migrationBuilder.Sql($@"
UPDATE ""{table}"" o SET ""Name"" = COALESCE((SELECT t.""Name"" FROM ""{translations}"" t WHERE t.""{key}"" = o.""Id""
    ORDER BY (t.""Language"" = 'en') DESC, t.""Language"" LIMIT 1), '');");
            migrationBuilder.Sql(@"
UPDATE ""AppSpaces"" o SET ""Note"" = (SELECT t.""Note"" FROM ""AppSpaceTranslations"" t WHERE t.""SpaceId"" = o.""Id""
    ORDER BY (t.""Language"" = 'en') DESC, t.""Language"" LIMIT 1);");

            migrationBuilder.DropTable(
                name: "AppBuildingTranslations");

            migrationBuilder.DropTable(
                name: "AppFloorTranslations");

            migrationBuilder.DropTable(
                name: "AppSpaceTranslations");

            migrationBuilder.DropTable(
                name: "AppSpaceTypeTranslations");

            migrationBuilder.CreateIndex(
                name: "IX_AppSpaceTypes_Name",
                table: "AppSpaceTypes",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppSpaces_Name",
                table: "AppSpaces",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppFloors_BuildingId_Name",
                table: "AppFloors",
                columns: new[] { "BuildingId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppBuildings_Name",
                table: "AppBuildings",
                column: "Name",
                unique: true);
        }
    }
}
