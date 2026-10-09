using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abstrict.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveFaceMatchScore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "face_match_score",
                table: "freelancer_profiles");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "face_match_score",
                table: "freelancer_profiles",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);
        }
    }
}
