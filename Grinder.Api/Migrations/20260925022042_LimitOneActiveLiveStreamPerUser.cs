using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Grinder.Api.Migrations
{
    /// <inheritdoc />
    public partial class LimitOneActiveLiveStreamPerUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Staging's table was renamed from Livestreams manually, which did
            // not rename its existing UserId index. Other databases may have
            // the new index name, so remove whichever version is present.
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS "IX_Livestreams_UserId";
                DROP INDEX IF EXISTS "IX_LiveStreams_UserId";
                """);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EndedAt",
                table: "LiveStreams",
                type: "timestamp with time zone",
                nullable: true);

            // Legacy rows have no end state because ending a stream did not update the DB.
            // Apply this migration when no broadcasts are in progress.
            migrationBuilder.Sql("""
                UPDATE "LiveStreams"
                SET "EndedAt" = "CreatedAt";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_LiveStreams_UserId",
                table: "LiveStreams",
                column: "UserId",
                unique: true,
                filter: "\"EndedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LiveStreams_UserId",
                table: "LiveStreams");

            migrationBuilder.DropColumn(
                name: "EndedAt",
                table: "LiveStreams");

            migrationBuilder.CreateIndex(
                name: "IX_LiveStreams_UserId",
                table: "LiveStreams",
                column: "UserId");
        }
    }
}
