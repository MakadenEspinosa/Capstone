using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ESEvents.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEventSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DepartmentId",
                table: "events",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EndsAtUtc",
                table: "events",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP + INTERVAL '1 hour'");

            migrationBuilder.AddColumn<bool>(
                name: "IsAllDay",
                table: "events",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StartsAtUtc",
                table: "events",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.CreateIndex(
                name: "IX_events_DepartmentId",
                table: "events",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_events_StartsAtUtc_EndsAtUtc",
                table: "events",
                columns: new[] { "StartsAtUtc", "EndsAtUtc" });

            migrationBuilder.AddForeignKey(
                name: "FK_events_departments_DepartmentId",
                table: "events",
                column: "DepartmentId",
                principalTable: "departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_events_departments_DepartmentId",
                table: "events");

            migrationBuilder.DropIndex(
                name: "IX_events_DepartmentId",
                table: "events");

            migrationBuilder.DropIndex(
                name: "IX_events_StartsAtUtc_EndsAtUtc",
                table: "events");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                table: "events");

            migrationBuilder.DropColumn(
                name: "EndsAtUtc",
                table: "events");

            migrationBuilder.DropColumn(
                name: "IsAllDay",
                table: "events");

            migrationBuilder.DropColumn(
                name: "StartsAtUtc",
                table: "events");
        }
    }
}
