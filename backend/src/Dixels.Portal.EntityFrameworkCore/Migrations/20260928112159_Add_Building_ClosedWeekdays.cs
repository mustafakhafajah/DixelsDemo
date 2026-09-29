using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dixels.Portal.Migrations
{
    /* Buildings can be closed every week on chosen days (e.g. Friday and Saturday); existing buildings start with none. */
    public partial class Add_Building_ClosedWeekdays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<int>>(
                name: "ClosedWeekdays",
                table: "AppBuildings",
                type: "integer[]",
                nullable: false,
                defaultValueSql: "'{}'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClosedWeekdays",
                table: "AppBuildings");
        }
    }
}
