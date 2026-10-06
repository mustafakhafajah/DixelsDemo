using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dixels.Portal.Migrations
{
    /* Idempotency keys are unique per person (OwnerUserId + key) instead of across everyone, so one person's
     * retry can never be answered with another person's booking.
     * Building and space names are no longer unique in the database: both are only soft-deleted, and their
     * left-behind name rows stopped anyone reusing a deleted one's name. BuildingManager / SpaceManager still
     * refuse a name another live building / space has; the indexes stay, non-unique, for those lookups. */
    public partial class Booking_Rule_Fixes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppSpaceTranslations_Language_Name",
                table: "AppSpaceTranslations");

            migrationBuilder.DropIndex(
                name: "IX_AppBuildingTranslations_Language_Name",
                table: "AppBuildingTranslations");

            migrationBuilder.DropIndex(
                name: "IX_AppBookings_IdempotencyKey",
                table: "AppBookings");

            migrationBuilder.CreateIndex(
                name: "IX_AppSpaceTranslations_Language_Name",
                table: "AppSpaceTranslations",
                columns: new[] { "Language", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_AppBuildingTranslations_Language_Name",
                table: "AppBuildingTranslations",
                columns: new[] { "Language", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_AppBookings_OwnerUserId_IdempotencyKey",
                table: "AppBookings",
                columns: new[] { "OwnerUserId", "IdempotencyKey" },
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppSpaceTranslations_Language_Name",
                table: "AppSpaceTranslations");

            migrationBuilder.DropIndex(
                name: "IX_AppBuildingTranslations_Language_Name",
                table: "AppBuildingTranslations");

            migrationBuilder.DropIndex(
                name: "IX_AppBookings_OwnerUserId_IdempotencyKey",
                table: "AppBookings");

            migrationBuilder.CreateIndex(
                name: "IX_AppSpaceTranslations_Language_Name",
                table: "AppSpaceTranslations",
                columns: new[] { "Language", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppBuildingTranslations_Language_Name",
                table: "AppBuildingTranslations",
                columns: new[] { "Language", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppBookings_IdempotencyKey",
                table: "AppBookings",
                column: "IdempotencyKey",
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");
        }
    }
}
