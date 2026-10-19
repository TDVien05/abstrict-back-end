using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abstrict.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddActiveDisputeBookingGuard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_disputes_booking_id",
                table: "disputes",
                column: "booking_id",
                unique: true,
                filter: "status IN ('Open', 'AwaitingResponse', 'UnderReview', 'Appealed')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_disputes_booking_id",
                table: "disputes");
        }
    }
}
