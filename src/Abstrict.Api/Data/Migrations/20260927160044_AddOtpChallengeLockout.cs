using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abstrict.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOtpChallengeLockout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_phone_otp_challenges_user_id_purpose_consumed_at_utc_expire~",
                table: "phone_otp_challenges");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "locked_at_utc",
                table: "phone_otp_challenges",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_phone_otp_challenges_user_id_purpose_consumed_at_utc_expire~",
                table: "phone_otp_challenges",
                columns: new[] { "user_id", "purpose", "consumed_at_utc", "expires_at_utc", "created_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_phone_otp_challenges_user_id_purpose_consumed_at_utc_expire~",
                table: "phone_otp_challenges");

            migrationBuilder.DropColumn(
                name: "locked_at_utc",
                table: "phone_otp_challenges");

            migrationBuilder.CreateIndex(
                name: "IX_phone_otp_challenges_user_id_purpose_consumed_at_utc_expire~",
                table: "phone_otp_challenges",
                columns: new[] { "user_id", "purpose", "consumed_at_utc", "expires_at_utc" });
        }
    }
}
