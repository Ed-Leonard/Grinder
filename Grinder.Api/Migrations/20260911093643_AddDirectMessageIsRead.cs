using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Grinder.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDirectMessageIsRead : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DirectMessages_Users_ReceiverId",
                table: "DirectMessages");

            migrationBuilder.DropForeignKey(
                name: "FK_DirectMessages_Users_SenderId",
                table: "DirectMessages");

            migrationBuilder.AlterColumn<string>(
                name: "Content",
                table: "DirectMessages",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<bool>(
                name: "IsRead",
                table: "DirectMessages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.DropIndex(
                name: "IX_DirectMessages_ReceiverId",
                table: "DirectMessages");

            migrationBuilder.CreateIndex(
                name: "IX_DirectMessages_ReceiverId_IsRead",
                table: "DirectMessages",
                columns: new[] { "ReceiverId", "IsRead" });

            migrationBuilder.AddForeignKey(
                name: "FK_DirectMessages_Users_ReceiverId",
                table: "DirectMessages",
                column: "ReceiverId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DirectMessages_Users_SenderId",
                table: "DirectMessages",
                column: "SenderId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DirectMessages_Users_ReceiverId",
                table: "DirectMessages");

            migrationBuilder.DropForeignKey(
                name: "FK_DirectMessages_Users_SenderId",
                table: "DirectMessages");

            migrationBuilder.DropIndex(
                name: "IX_DirectMessages_ReceiverId_IsRead",
                table: "DirectMessages");

            migrationBuilder.CreateIndex(
                name: "IX_DirectMessages_ReceiverId",
                table: "DirectMessages",
                column: "ReceiverId");

            migrationBuilder.DropColumn(
                name: "IsRead",
                table: "DirectMessages");

            migrationBuilder.AlterColumn<string>(
                name: "Content",
                table: "DirectMessages",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(2048)",
                oldMaxLength: 2048);

            migrationBuilder.AddForeignKey(
                name: "FK_DirectMessages_Users_ReceiverId",
                table: "DirectMessages",
                column: "ReceiverId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DirectMessages_Users_SenderId",
                table: "DirectMessages",
                column: "SenderId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
