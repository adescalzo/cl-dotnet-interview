using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TodoApi.Migrations
{
    /// <inheritdoc />
    public partial class AddSyncModule : Migration
    {
        private static readonly string[] SyncEventEntityColumns = ["EntityType", "EntityId"];
        private static readonly string[] SyncMappingExternalColumns = ["EntityType", "ExternalId"];
        private static readonly string[] SyncMappingLocalColumns = ["EntityType", "LocalId"];

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

            migrationBuilder.AddColumn<Guid>(
                name: "SyncId",
                table: "TodoItem",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000")
            );

            migrationBuilder.CreateTable(
                name: "SyncEvent",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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

            migrationBuilder.CreateTable(
                name: "SyncMapping",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityType = table.Column<int>(type: "int", nullable: false),
                    LocalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalId = table.Column<string>(
                        type: "nvarchar(500)",
                        maxLength: 500,
                        nullable: false
                    ),
                    ExternalUpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSyncedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncMapping", x => x.Id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_TodoItem_SyncId",
                table: "TodoItem",
                column: "SyncId",
                unique: true
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

            migrationBuilder.CreateIndex(
                name: "IX_SyncMapping_EntityType_ExternalId",
                table: "SyncMapping",
                columns: SyncMappingExternalColumns,
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_SyncMapping_EntityType_LocalId",
                table: "SyncMapping",
                columns: SyncMappingLocalColumns,
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "SyncEvent");

            migrationBuilder.DropTable(name: "SyncMapping");

            migrationBuilder.DropIndex(name: "IX_TodoItem_SyncId", table: "TodoItem");

            migrationBuilder.DropColumn(name: "CreatedAt", table: "TodoList");

            migrationBuilder.DropColumn(name: "SyncId", table: "TodoItem");
        }
    }
}
