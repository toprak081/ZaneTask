using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZaneTask.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BoardColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tasks_ProjectId_Status_Position",
                table: "Tasks");

            migrationBuilder.AddColumn<Guid>(
                name: "ColumnId",
                table: "Tasks",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "BoardColumns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoardColumns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BoardColumns_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_ColumnId_Position",
                table: "Tasks",
                columns: new[] { "ColumnId", "Position" });

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_ProjectId_Status",
                table: "Tasks",
                columns: new[] { "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_BoardColumns_ProjectId_Position",
                table: "BoardColumns",
                columns: new[] { "ProjectId", "Position" });

            // Existing projects get the three default columns; each task goes to the column matching its status.
            migrationBuilder.Sql("""
                INSERT INTO "BoardColumns" ("Id", "ProjectId", "Name", "Position", "Category")
                SELECT gen_random_uuid(), p."Id", c.name, c.pos, c.cat
                FROM "Projects" AS p
                CROSS JOIN (VALUES ('To do', 0, 'Todo'), ('In progress', 1, 'InProgress'), ('Done', 2, 'Done')) AS c(name, pos, cat);
                """);
            migrationBuilder.Sql("""
                UPDATE "Tasks" AS t SET "ColumnId" = c."Id"
                FROM "BoardColumns" AS c
                WHERE c."ProjectId" = t."ProjectId" AND c."Category" = t."Status";
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_Tasks_BoardColumns_ColumnId",
                table: "Tasks",
                column: "ColumnId",
                principalTable: "BoardColumns",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tasks_BoardColumns_ColumnId",
                table: "Tasks");

            migrationBuilder.DropTable(
                name: "BoardColumns");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_ColumnId_Position",
                table: "Tasks");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_ProjectId_Status",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "ColumnId",
                table: "Tasks");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_ProjectId_Status_Position",
                table: "Tasks",
                columns: new[] { "ProjectId", "Status", "Position" });
        }
    }
}
