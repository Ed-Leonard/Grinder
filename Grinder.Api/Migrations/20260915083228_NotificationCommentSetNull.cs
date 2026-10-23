using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Grinder.Api.Migrations
{
    /// <inheritdoc />
    public partial class NotificationCommentSetNull : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notification_Comments_CommentId",
                table: "Notification");

            migrationBuilder.AddForeignKey(
                name: "FK_Notification_Comments_CommentId",
                table: "Notification",
                column: "CommentId",
                principalTable: "Comments",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notification_Comments_CommentId",
                table: "Notification");

            migrationBuilder.AddForeignKey(
                name: "FK_Notification_Comments_CommentId",
                table: "Notification",
                column: "CommentId",
                principalTable: "Comments",
                principalColumn: "Id");
        }
    }
}
