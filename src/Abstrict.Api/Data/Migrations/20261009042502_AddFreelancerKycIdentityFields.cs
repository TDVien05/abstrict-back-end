using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abstrict.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFreelancerKycIdentityFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "citizen_id_hash",
                table: "freelancer_profiles",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "citizen_id_last4",
                table: "freelancer_profiles",
                type: "character varying(4)",
                maxLength: 4,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "citizen_id_number_protected",
                table: "freelancer_profiles",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_freelancer_profiles_citizen_id_hash",
                table: "freelancer_profiles",
                column: "citizen_id_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_freelancer_profiles_citizen_id_hash",
                table: "freelancer_profiles");

            migrationBuilder.DropColumn(
                name: "citizen_id_hash",
                table: "freelancer_profiles");

            migrationBuilder.DropColumn(
                name: "citizen_id_last4",
                table: "freelancer_profiles");

            migrationBuilder.DropColumn(
                name: "citizen_id_number_protected",
                table: "freelancer_profiles");
        }
    }
}
