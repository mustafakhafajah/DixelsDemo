using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dixels.Portal.Migrations
{
    /* Every record must have an English name now (other languages are optional extras). A record that was only
     * ever named in another language gets that name copied into its English row, so it still shows somewhere;
     * an admin should then give it a real English name. */
    /// <inheritdoc />
    public partial class Require_English_Names : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (translations, key, columns) in new[]
                     {
                         ("AppBuildingTranslations", "BuildingId", @"""Name"""),
                         ("AppFloorTranslations", "FloorId", @"""Name"""),
                         ("AppSpaceTypeTranslations", "SpaceTypeId", @"""Name"""),
                         ("AppSpaceTranslations", "SpaceId", @"""Name"", ""Note"""),
                     })
                migrationBuilder.Sql($@"
INSERT INTO ""{translations}"" (""{key}"", ""Language"", {columns})
SELECT DISTINCT ON (t.""{key}"") t.""{key}"", 'en', {Prefixed(columns)}
FROM ""{translations}"" t
WHERE NOT EXISTS (SELECT 1 FROM ""{translations}"" e WHERE e.""{key}"" = t.""{key}"" AND e.""Language"" = 'en')
ORDER BY t.""{key}"", t.""Language"";");
        }

        /* Nothing to undo: the copied English rows are ordinary names. */
        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }

        private static string Prefixed(string columns) => columns.Replace(@"""Name""", @"t.""Name""").Replace(@"""Note""", @"t.""Note""");
    }
}
