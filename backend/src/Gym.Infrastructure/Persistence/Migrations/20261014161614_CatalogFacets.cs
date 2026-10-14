using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gym.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CatalogFacets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TargetAudienceOverride",
                table: "Gyms",
                type: "character varying(24)",
                maxLength: 24,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TrainingTypesOverride",
                table: "Gyms",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TargetAudience",
                table: "GymChains",
                type: "character varying(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "General");

            migrationBuilder.AddColumn<int>(
                name: "TrainingTypes",
                table: "GymChains",
                type: "integer",
                nullable: false,
                defaultValue: 7);

            migrationBuilder.Sql(
                """
                UPDATE "GymChains"
                SET "TrainingTypes" = CASE "Slug"
                    WHEN 'john-harris-fitness' THEN 207
                    WHEN 'holmes-place' THEN 207
                    WHEN 'mrs-sporty' THEN 45
                    WHEN 'john-reed' THEN 15
                    WHEN 'golds-gym' THEN 207
                    WHEN 'manhattan-fitness-club' THEN 207
                    WHEN 'get-up-fitness-og' THEN 203
                    WHEN 'injoy-health-club-wien' THEN 207
                    WHEN 'isc-gym' THEN 71
                    WHEN 'bodystyle' THEN 203
                    WHEN 'speedfit' THEN 15
                    WHEN 'fitness-club-39' THEN 3
                    WHEN 'marco-polo-sportcenter' THEN 67
                    WHEN 'crossfit-viertel-zwei' THEN 95
                    WHEN 'dasgym' THEN 15
                    ELSE 7
                END,
                "TargetAudience" = CASE
                    WHEN "Slug" = 'mrs-sporty' THEN 'WomenOnly'
                    ELSE 'General'
                END;

                UPDATE "Gyms"
                SET "TrainingTypesOverride" = 31,
                    "TargetAudienceOverride" = 'General'
                WHERE "Slug" = 'crossfit-vienna-the-starship';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TargetAudienceOverride",
                table: "Gyms");

            migrationBuilder.DropColumn(
                name: "TrainingTypesOverride",
                table: "Gyms");

            migrationBuilder.DropColumn(
                name: "TargetAudience",
                table: "GymChains");

            migrationBuilder.DropColumn(
                name: "TrainingTypes",
                table: "GymChains");
        }
    }
}
