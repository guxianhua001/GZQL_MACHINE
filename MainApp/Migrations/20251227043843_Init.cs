using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MainApp.Migrations
{
    /// <inheritdoc />
    public partial class Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Alarms",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Timestamp = table.Column<DateTime>(type: "datetime2(4)", precision: 4, nullable: false),
                    StationId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OriginalRawTime = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Duration = table.Column<TimeSpan>(type: "time", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Alarms", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Alarms_Id",
                table: "Alarms",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_Alarms_Level",
                table: "Alarms",
                column: "Level");

            migrationBuilder.CreateIndex(
                name: "IX_Alarms_StationId",
                table: "Alarms",
                column: "StationId");

            migrationBuilder.CreateIndex(
                name: "IX_Alarms_Timestamp",
                table: "Alarms",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_Alarms_Timestamp_Id",
                table: "Alarms",
                columns: new[] { "Timestamp", "Id" },
                descending: new[] { false, true })
                .Annotation("SqlServer:FillFactor", 90);

            migrationBuilder.CreateIndex(
                name: "IX_Alarms_Timestamp_Level",
                table: "Alarms",
                columns: new[] { "Timestamp", "Level" })
                .Annotation("SqlServer:Include", new[] { "StationId", "Code" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Alarms");
        }
    }
}
