using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dixels.Portal.Migrations
{
    /* 1. Space types move from a fixed enum (int column "Type") to an admin-managed table.
     *    Existing spaces keep their type: the four old enum values become four rows with fixed IDs.
     * 2. A space no longer stores its own time zone; it always uses its building's.
     * Hand-edited: the generated version dropped "Type" without moving the values and filled
     * "TypeId" with an empty GUID, which the new foreign key would reject.
     * The IDs are written out here (not taken from DefaultSpaceTypes) so this migration never changes. */
    public partial class Add_SpaceTypes_And_Inherit_Building_TimeZone : Migration
    {
        private const string MeetingRoom = "5c0e3a1e-0b6f-4d2a-9a51-000000000001";
        private const string Equipment = "5c0e3a1e-0b6f-4d2a-9a51-000000000002";
        private const string Desk = "5c0e3a1e-0b6f-4d2a-9a51-000000000003";
        private const string Studio = "5c0e3a1e-0b6f-4d2a-9a51-000000000004";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppSpaceTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSpaceTypes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppSpaceTypes_Name",
                table: "AppSpaceTypes",
                column: "Name",
                unique: true);

            /* The four types the enum used to have, in enum order (0..3). */
            migrationBuilder.Sql($@"
INSERT INTO ""AppSpaceTypes"" (""Id"", ""Name"", ""ExtraProperties"", ""ConcurrencyStamp"", ""CreationTime"") VALUES
  ('{MeetingRoom}', 'Meeting room', '{{}}', md5(random()::text), now() at time zone 'utc'),
  ('{Equipment}',   'Equipment',    '{{}}', md5(random()::text), now() at time zone 'utc'),
  ('{Desk}',        'Desk',         '{{}}', md5(random()::text), now() at time zone 'utc'),
  ('{Studio}',      'Studio',       '{{}}', md5(random()::text), now() at time zone 'utc');");

            /* Add TypeId as nullable, fill it from the old enum value, then make it required. */
            migrationBuilder.AddColumn<Guid>(
                name: "TypeId",
                table: "AppSpaces",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql($@"
UPDATE ""AppSpaces"" SET ""TypeId"" = (CASE ""Type""
    WHEN 1 THEN '{Equipment}'
    WHEN 2 THEN '{Desk}'
    WHEN 3 THEN '{Studio}'
    ELSE '{MeetingRoom}' END)::uuid;");

            migrationBuilder.AlterColumn<Guid>(
                name: "TypeId",
                table: "AppSpaces",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "Type",
                table: "AppSpaces");

            /* A space's time zone now always comes from its building. */
            migrationBuilder.DropColumn(
                name: "TimeZone",
                table: "AppSpaces");

            migrationBuilder.CreateIndex(
                name: "IX_AppSpaces_TypeId",
                table: "AppSpaces",
                column: "TypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppSpaces_AppSpaceTypes_TypeId",
                table: "AppSpaces",
                column: "TypeId",
                principalTable: "AppSpaceTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "AppSpaces",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "TimeZone",
                table: "AppSpaces",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "UTC");

            /* Types added in the admin page have no enum value; they fall back to Meeting room (0). */
            migrationBuilder.Sql($@"
UPDATE ""AppSpaces"" SET ""Type"" = CASE ""TypeId""
    WHEN '{Equipment}' THEN 1
    WHEN '{Desk}' THEN 2
    WHEN '{Studio}' THEN 3
    ELSE 0 END;
UPDATE ""AppSpaces"" s SET ""TimeZone"" = b.""TimeZone"" FROM ""AppBuildings"" b WHERE b.""Id"" = s.""BuildingId"";");

            migrationBuilder.DropForeignKey(
                name: "FK_AppSpaces_AppSpaceTypes_TypeId",
                table: "AppSpaces");

            migrationBuilder.DropIndex(
                name: "IX_AppSpaces_TypeId",
                table: "AppSpaces");

            migrationBuilder.DropColumn(
                name: "TypeId",
                table: "AppSpaces");

            migrationBuilder.DropTable(
                name: "AppSpaceTypes");
        }
    }
}
