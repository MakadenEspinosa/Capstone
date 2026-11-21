using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ESEvents.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyOwnershipFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "events",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "departments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_events_CompanyId",
                table: "events",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_departments_CompanyId",
                table: "departments",
                column: "CompanyId");

            migrationBuilder.AddForeignKey(
                name: "FK_departments_companies_CompanyId",
                table: "departments",
                column: "CompanyId",
                principalTable: "companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_events_companies_CompanyId",
                table: "events",
                column: "CompanyId",
                principalTable: "companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_departments_companies_CompanyId",
                table: "departments");

            migrationBuilder.DropForeignKey(
                name: "FK_events_companies_CompanyId",
                table: "events");

            migrationBuilder.DropIndex(
                name: "IX_events_CompanyId",
                table: "events");

            migrationBuilder.DropIndex(
                name: "IX_departments_CompanyId",
                table: "departments");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "events");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "departments");
        }
    }
}
