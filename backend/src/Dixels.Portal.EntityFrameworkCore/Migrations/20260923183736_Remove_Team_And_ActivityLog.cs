using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dixels.Portal.Migrations
{
    /// <inheritdoc />
    public partial class Remove_Team_And_ActivityLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppActivityLogEntries");

            migrationBuilder.DropTable(
                name: "AppTeams");

            migrationBuilder.DropColumn(
                name: "RestrictedTeamIds",
                table: "AppSpaces");

            migrationBuilder.DropColumn(
                name: "TeamId",
                table: "AbpUsers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<Guid>>(
                name: "RestrictedTeamIds",
                table: "AppSpaces",
                type: "uuid[]",
                nullable: false);

            migrationBuilder.AddColumn<Guid>(
                name: "TeamId",
                table: "AbpUsers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AppActivityLogEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ActorName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Detail = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    EntityId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppActivityLogEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppTeams",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppTeams", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppActivityLogEntries_EntityType_EntityId",
                table: "AppActivityLogEntries",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppActivityLogEntries_TimestampUtc",
                table: "AppActivityLogEntries",
                column: "TimestampUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AppTeams_Name",
                table: "AppTeams",
                column: "Name",
                unique: true);
        }
    }
}
