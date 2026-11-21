using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ESEvents.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompanyOnboarding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                  IF EXISTS (SELECT 1 FROM company_members GROUP BY "UserId" HAVING COUNT(*) > 1) THEN
                    RAISE EXCEPTION 'Company onboarding migration requires resolving users with multiple company memberships first. No memberships were changed.';
                  END IF;
                END $$;
                """);
            migrationBuilder.DropIndex(
                name: "IX_company_members_UserId",
                table: "company_members");

            migrationBuilder.AddColumn<int>(
                name: "Role",
                table: "company_members",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "AdminCodeHash",
                table: "companies",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MemberCodeHash",
                table: "companies",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_company_members_UserId",
                table: "company_members",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_companies_AdminCodeHash",
                table: "companies",
                column: "AdminCodeHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_companies_MemberCodeHash",
                table: "companies",
                column: "MemberCodeHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_company_members_UserId",
                table: "company_members");

            migrationBuilder.DropIndex(
                name: "IX_companies_AdminCodeHash",
                table: "companies");

            migrationBuilder.DropIndex(
                name: "IX_companies_MemberCodeHash",
                table: "companies");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "company_members");

            migrationBuilder.DropColumn(
                name: "AdminCodeHash",
                table: "companies");

            migrationBuilder.DropColumn(
                name: "MemberCodeHash",
                table: "companies");

            migrationBuilder.CreateIndex(
                name: "IX_company_members_UserId",
                table: "company_members",
                column: "UserId");
        }
    }
}
