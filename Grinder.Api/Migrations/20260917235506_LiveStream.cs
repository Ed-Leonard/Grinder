using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Grinder.Api.Migrations
{
    /// <inheritdoc />
    public partial class LiveStream : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "Livestreams",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Livestreams_UserId",
                table: "Livestreams",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Livestreams_Users_UserId",
                table: "Livestreams",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Livestreams_Users_UserId",
                table: "Livestreams");

            migrationBuilder.DropIndex(
                name: "IX_Livestreams_UserId",
                table: "Livestreams");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Livestreams");
        }
    }
}
