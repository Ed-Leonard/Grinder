using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Grinder.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskCompletedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CompletedAt",
                table: "TaskItems",
                type: "timestamp with time zone",
                nullable: true);

            // Best-effort backfill: existing Done tasks have no completion timestamp,
            // so use UpdatedAt (set when status last changed, and on later edits).
            migrationBuilder.Sql(
                """
                UPDATE "TaskItems"
                SET "CompletedAt" = "UpdatedAt"
                WHERE "ItemStatus" = 2 AND "CompletedAt" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "TaskItems");
        }
    }
}
