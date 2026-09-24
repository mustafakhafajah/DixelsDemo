using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dixels.Portal.Migrations
{
    /// <inheritdoc />
    public partial class Add_Estate_Domain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                    TimestampUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Action = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EntityId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Detail = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppActivityLogEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppBuildings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TimeZone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    OpenHour = table.Column<int>(type: "integer", nullable: false),
                    CloseHour = table.Column<int>(type: "integer", nullable: false),
                    MinBookingMinutes = table.Column<int>(type: "integer", nullable: false),
                    MaxBookingHours = table.Column<int>(type: "integer", nullable: false),
                    Holidays = table.Column<List<DateOnly>>(type: "date[]", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppBuildings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppTeams",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppTeams", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppFloors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BuildingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    OpenHourOverride = table.Column<int>(type: "integer", nullable: true),
                    CloseHourOverride = table.Column<int>(type: "integer", nullable: true),
                    MinBookingMinutesOverride = table.Column<int>(type: "integer", nullable: true),
                    MaxBookingHoursOverride = table.Column<int>(type: "integer", nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppFloors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppFloors_AppBuildings_BuildingId",
                        column: x => x.BuildingId,
                        principalTable: "AppBuildings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppSpaces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    BuildingId = table.Column<Guid>(type: "uuid", nullable: false),
                    FloorId = table.Column<Guid>(type: "uuid", nullable: false),
                    TimeZone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    RestrictedTeamIds = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OpenHourOverride = table.Column<int>(type: "integer", nullable: true),
                    CloseHourOverride = table.Column<int>(type: "integer", nullable: true),
                    MinBookingMinutesOverride = table.Column<int>(type: "integer", nullable: true),
                    MaxBookingHoursOverride = table.Column<int>(type: "integer", nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSpaces", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppSpaces_AppBuildings_BuildingId",
                        column: x => x.BuildingId,
                        principalTable: "AppBuildings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppSpaces_AppFloors_FloorId",
                        column: x => x.FloorId,
                        principalTable: "AppFloors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppBookings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SpaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    SeriesId = table.Column<Guid>(type: "uuid", nullable: true),
                    Parking = table.Column<bool>(type: "boolean", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppBookings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppBookings_AppSpaces_SpaceId",
                        column: x => x.SpaceId,
                        principalTable: "AppSpaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppMaintenanceWindows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SpaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SeriesId = table.Column<Guid>(type: "uuid", nullable: true),
                    ScopeType = table.Column<int>(type: "integer", nullable: false),
                    ScopeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppMaintenanceWindows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppMaintenanceWindows_AppSpaces_SpaceId",
                        column: x => x.SpaceId,
                        principalTable: "AppSpaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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
                name: "IX_AppBookings_IdempotencyKey",
                table: "AppBookings",
                column: "IdempotencyKey",
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AppBookings_OwnerUserId_Status_StartUtc_EndUtc",
                table: "AppBookings",
                columns: new[] { "OwnerUserId", "Status", "StartUtc", "EndUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AppBookings_SeriesId",
                table: "AppBookings",
                column: "SeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_AppBookings_SpaceId_Status_StartUtc_EndUtc",
                table: "AppBookings",
                columns: new[] { "SpaceId", "Status", "StartUtc", "EndUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AppBuildings_Name",
                table: "AppBuildings",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppFloors_BuildingId_Name",
                table: "AppFloors",
                columns: new[] { "BuildingId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppMaintenanceWindows_ScopeType_ScopeId",
                table: "AppMaintenanceWindows",
                columns: new[] { "ScopeType", "ScopeId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppMaintenanceWindows_SeriesId",
                table: "AppMaintenanceWindows",
                column: "SeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_AppMaintenanceWindows_SpaceId_Status_StartUtc_EndUtc",
                table: "AppMaintenanceWindows",
                columns: new[] { "SpaceId", "Status", "StartUtc", "EndUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AppSpaces_BuildingId_FloorId_Status",
                table: "AppSpaces",
                columns: new[] { "BuildingId", "FloorId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AppSpaces_FloorId",
                table: "AppSpaces",
                column: "FloorId");

            migrationBuilder.CreateIndex(
                name: "IX_AppSpaces_Name",
                table: "AppSpaces",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppTeams_Name",
                table: "AppTeams",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppActivityLogEntries");

            migrationBuilder.DropTable(
                name: "AppBookings");

            migrationBuilder.DropTable(
                name: "AppMaintenanceWindows");

            migrationBuilder.DropTable(
                name: "AppTeams");

            migrationBuilder.DropTable(
                name: "AppSpaces");

            migrationBuilder.DropTable(
                name: "AppFloors");

            migrationBuilder.DropTable(
                name: "AppBuildings");

            migrationBuilder.DropColumn(
                name: "TeamId",
                table: "AbpUsers");
        }
    }
}
