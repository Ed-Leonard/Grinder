using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Grinder.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CommentId",
                table: "Notification",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReactionId",
                table: "Notification",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notification_CommentId",
                table: "Notification",
                column: "CommentId");

            migrationBuilder.CreateIndex(
                name: "IX_Notification_ReactionId",
                table: "Notification",
                column: "ReactionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Notification_Comments_CommentId",
                table: "Notification",
                column: "CommentId",
                principalTable: "Comments",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Notification_Reactions_ReactionId",
                table: "Notification",
                column: "ReactionId",
                principalTable: "Reactions",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notification_Comments_CommentId",
                table: "Notification");

            migrationBuilder.DropForeignKey(
                name: "FK_Notification_Reactions_ReactionId",
                table: "Notification");

            migrationBuilder.DropIndex(
                name: "IX_Notification_CommentId",
                table: "Notification");

            migrationBuilder.DropIndex(
                name: "IX_Notification_ReactionId",
                table: "Notification");

            migrationBuilder.DropColumn(
                name: "CommentId",
                table: "Notification");

            migrationBuilder.DropColumn(
                name: "ReactionId",
                table: "Notification");
        }
    }
}
