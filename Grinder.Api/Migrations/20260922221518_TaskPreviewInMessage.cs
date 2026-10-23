using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Grinder.Api.Migrations
{
	/// <inheritdoc />
	public partial class TaskPreviewInMessage : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AlterColumn<string>(
				name: "Content",
				table: "DirectMessages",
				type: "character varying(256)",
				maxLength: 256,
				nullable: true,
				oldClrType: typeof(string),
				oldType: "character varying(2048)",
				oldMaxLength: 2048);

			migrationBuilder.AddColumn<int>(
				name: "SharedTaskId",
				table: "DirectMessages",
				type: "integer",
				nullable: true);

			migrationBuilder.CreateIndex(
				name: "IX_DirectMessages_SharedTaskId",
				table: "DirectMessages",
				column: "SharedTaskId");

			migrationBuilder.AddForeignKey(
				name: "FK_DirectMessages_TaskItems_SharedTaskId",
				table: "DirectMessages",
				column: "SharedTaskId",
				principalTable: "TaskItems",
				principalColumn: "Id");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropForeignKey(
				name: "FK_DirectMessages_TaskItems_SharedTaskId",
				table: "DirectMessages");

			migrationBuilder.DropIndex(
				name: "IX_DirectMessages_SharedTaskId",
				table: "DirectMessages");

			migrationBuilder.DropColumn(
				name: "CompletedAt",
				table: "TaskItems");

			migrationBuilder.DropColumn(
				name: "SharedTaskId",
				table: "DirectMessages");

			migrationBuilder.AlterColumn<string>(
				name: "Content",
				table: "DirectMessages",
				type: "character varying(2048)",
				maxLength: 2048,
				nullable: false,
				defaultValue: "",
				oldClrType: typeof(string),
				oldType: "character varying(256)",
				oldMaxLength: 256,
				oldNullable: true);
		}
	}
}
