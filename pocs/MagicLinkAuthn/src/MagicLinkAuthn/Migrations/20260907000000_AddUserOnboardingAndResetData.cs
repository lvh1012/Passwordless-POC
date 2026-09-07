using MagicLinkAuthn.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MagicLinkAuthn.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260907000000_AddUserOnboardingAndResetData")]
public sealed class AddUserOnboardingAndResetData : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // This reset was explicitly approved for the Magic Link POC. Keep migration history so the
        // operation runs once, but invalidate every account, login session, token and pending email.
        migrationBuilder.Sql(
            """
            TRUNCATE TABLE
                "MagicLinkOutboxMessages",
                "MagicLinkRequests",
                "AspNetRoleClaims",
                "AspNetUserClaims",
                "AspNetUserLogins",
                "AspNetUserRoles",
                "AspNetUserTokens",
                "AspNetRoles",
                "AspNetUsers",
                "DataProtectionKeys"
            RESTART IDENTITY CASCADE;
            """);

        migrationBuilder.AddColumn<string>(
            name: "FullName",
            table: "AspNetUsers",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "ProfileCompletedAt",
            table: "AspNetUsers",
            type: "timestamp with time zone",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "FullName", table: "AspNetUsers");
        migrationBuilder.DropColumn(name: "ProfileCompletedAt", table: "AspNetUsers");
    }
}
