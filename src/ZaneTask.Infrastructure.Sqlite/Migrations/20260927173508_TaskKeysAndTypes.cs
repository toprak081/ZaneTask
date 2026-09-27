using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZaneTask.Infrastructure.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class TaskKeysAndTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Number",
                table: "Tasks",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "Tasks",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "Task");

            migrationBuilder.AddColumn<string>(
                name: "Key",
                table: "Projects",
                type: "TEXT",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "NextTaskNumber",
                table: "Projects",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            // Existing data: projects get keys P1, P2… (by creation order; renameable in settings) and tasks are
            // numbered 1, 2, 3… per project by creation order. Runs before the unique indexes are created.
            migrationBuilder.Sql("""
                UPDATE "Projects" AS p SET "Key" = 'P' || n.rn
                FROM (SELECT "Id", ROW_NUMBER() OVER (ORDER BY "CreatedAt", "Id") AS rn FROM "Projects") AS n
                WHERE p."Id" = n."Id";
                """);
            migrationBuilder.Sql("""
                UPDATE "Tasks" AS t SET "Number" = n.rn
                FROM (SELECT "Id", ROW_NUMBER() OVER (PARTITION BY "ProjectId" ORDER BY "CreatedAt", "Id") AS rn FROM "Tasks") AS n
                WHERE t."Id" = n."Id";
                """);
            migrationBuilder.Sql("""
                UPDATE "Projects" SET "NextTaskNumber" =
                    COALESCE((SELECT MAX(t."Number") FROM "Tasks" AS t WHERE t."ProjectId" = "Projects"."Id"), 0) + 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_ProjectId_Number",
                table: "Tasks",
                columns: new[] { "ProjectId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_Key",
                table: "Projects",
                column: "Key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tasks_ProjectId_Number",
                table: "Tasks");

            migrationBuilder.DropIndex(
                name: "IX_Projects_Key",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "Number",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "Key",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "NextTaskNumber",
                table: "Projects");
        }
    }
}
