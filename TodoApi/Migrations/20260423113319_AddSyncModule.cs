using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TodoApi.Migrations
{
    /// <inheritdoc />
    public partial class AddSyncModule : Migration
    {
        private static readonly string[] SyncEventEntityColumns = ["EntityType", "EntityId"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "TodoList",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified)
            );

            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "TodoList",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true
            );

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
                name: "CompletedAt",
                table: "TodoItem",
                type: "datetime2",
                nullable: true
            );

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "TodoItem",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified)
            );

            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "TodoItem",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "Order",
                table: "TodoItem",
                type: "int",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.CreateTable(
                name: "SyncEvent",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityType = table.Column<int>(type: "int", nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<int>(type: "int", nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(max)", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncEvent", x => x.Id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_TodoList_ExternalId",
                table: "TodoList",
                column: "ExternalId",
                unique: true,
                filter: "[ExternalId] IS NOT NULL"
            );

            migrationBuilder.CreateIndex(
                name: "IX_TodoItem_ExternalId",
                table: "TodoItem",
                column: "ExternalId",
                unique: true,
                filter: "[ExternalId] IS NOT NULL"
            );

            migrationBuilder.CreateIndex(
                name: "IX_SyncEvent_EntityType_EntityId",
                table: "SyncEvent",
                columns: SyncEventEntityColumns
            );

            migrationBuilder.CreateIndex(
                name: "IX_SyncEvent_Status",
                table: "SyncEvent",
                column: "Status"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "SyncEvent");

            migrationBuilder.DropIndex(name: "IX_TodoList_ExternalId", table: "TodoList");

            migrationBuilder.DropIndex(name: "IX_TodoItem_ExternalId", table: "TodoItem");

            migrationBuilder.DropColumn(name: "CreatedAt", table: "TodoList");

            migrationBuilder.DropColumn(name: "ExternalId", table: "TodoList");

            migrationBuilder.DropColumn(name: "CompletedAt", table: "TodoItem");

            migrationBuilder.DropColumn(name: "CreatedAt", table: "TodoItem");

            migrationBuilder.DropColumn(name: "ExternalId", table: "TodoItem");

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
        }
    }
}
