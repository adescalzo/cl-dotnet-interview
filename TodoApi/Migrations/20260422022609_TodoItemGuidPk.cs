using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TodoApi.Migrations
{
    /// <inheritdoc />
    public partial class TodoItemGuidPk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_TodoItem_SyncId", table: "TodoItem");

            migrationBuilder.DropColumn(name: "SyncId", table: "TodoItem");

            migrationBuilder
                .AlterColumn<Guid>(
                    name: "Id",
                    table: "TodoItem",
                    type: "uniqueidentifier",
                    nullable: false,
                    oldClrType: typeof(long),
                    oldType: "bigint"
                )
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "TodoItem",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified)
            );

            migrationBuilder.AddColumn<int>(
                name: "Order",
                table: "TodoItem",
                type: "int",
                nullable: false,
                defaultValue: 0
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "CreatedAt", table: "TodoItem");

            migrationBuilder.DropColumn(name: "Order", table: "TodoItem");

            migrationBuilder
                .AlterColumn<long>(
                    name: "Id",
                    table: "TodoItem",
                    type: "bigint",
                    nullable: false,
                    oldClrType: typeof(Guid),
                    oldType: "uniqueidentifier"
                )
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddColumn<Guid>(
                name: "SyncId",
                table: "TodoItem",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000")
            );

            migrationBuilder.CreateIndex(
                name: "IX_TodoItem_SyncId",
                table: "TodoItem",
                column: "SyncId",
                unique: true
            );
        }
    }
}
