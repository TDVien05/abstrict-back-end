using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abstrict.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingOverlapGuard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Chặn hai booking đang chiếm slot của cùng một provider chồng giờ nhau ở cấp database.
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist;");
            migrationBuilder.Sql("""
                ALTER TABLE bookings ADD CONSTRAINT ex_bookings_provider_slot
                    EXCLUDE USING gist (provider_id WITH =, tstzrange(starts_at_utc, ends_at_utc, '[)') WITH &&)
                    WHERE (status IN ('PendingDeposit','PendingProviderConfirmation','Confirmed','CheckedIn','InProgress','PendingAcceptance'));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE bookings DROP CONSTRAINT IF EXISTS ex_bookings_provider_slot;");
        }
    }
}
