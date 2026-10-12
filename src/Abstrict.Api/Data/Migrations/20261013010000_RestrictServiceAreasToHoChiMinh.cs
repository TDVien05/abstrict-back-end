using System;
using Abstrict.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abstrict.Api.Data.Migrations
{
    /// <summary>
    /// Giới hạn khu vực dịch vụ ở TP. Hồ Chí Minh: ngưng khu vực ngoài TP.HCM (giữ hàng để không vỡ khóa ngoại),
    /// chuẩn hóa tên quận theo dữ liệu hành chính và bổ sung đủ 22 quận/huyện/thành phố thuộc TP.HCM.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261013010000_RestrictServiceAreasToHoChiMinh")]
    public partial class RestrictServiceAreasToHoChiMinh : Migration
    {
        private static readonly DateTimeOffset SeededAt = new(2026, 10, 13, 0, 0, 0, TimeSpan.Zero);

        private static Guid AreaId(int n) => new($"bbbbbbbb-0000-0000-0000-{n:D12}");

        // Quận/huyện chưa có trong SeedCatalogData; id 11 trở đi (1-10 đã dùng).
        private static readonly string[] NewDistricts =
        [
            "Quận 12", "Quận Gò Vấp", "Quận Tân Phú", "Quận Phú Nhuận", "Thành phố Thủ Đức", "Quận 10", "Quận 11", "Quận 4", "Quận 5",
            "Quận 6", "Quận 8", "Quận Bình Tân", "Huyện Củ Chi", "Huyện Hóc Môn", "Huyện Bình Chánh", "Huyện Nhà Bè", "Huyện Cần Giờ"
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE service_areas SET is_active = false, updated_at_utc = now() WHERE city <> 'Hồ Chí Minh';");
            migrationBuilder.Sql($"UPDATE service_areas SET district = 'Quận 7', ward_or_complex = NULL, updated_at_utc = now() WHERE id = '{AreaId(3)}';");
            migrationBuilder.Sql($"UPDATE service_areas SET district = 'Quận Bình Thạnh', updated_at_utc = now() WHERE id = '{AreaId(4)}';");
            migrationBuilder.Sql($"UPDATE service_areas SET district = 'Quận Tân Bình', updated_at_utc = now() WHERE id = '{AreaId(5)}';");

            var rows = new object[NewDistricts.Length, 7];
            for (var i = 0; i < NewDistricts.Length; i++)
            {
                rows[i, 0] = AreaId(11 + i);
                rows[i, 1] = "Hồ Chí Minh";
                rows[i, 2] = NewDistricts[i];
                rows[i, 3] = null;
                rows[i, 4] = true;
                rows[i, 5] = SeededAt;
                rows[i, 6] = SeededAt;
            }

            migrationBuilder.InsertData(
                table: "service_areas",
                columns: new[] { "id", "city", "district", "ward_or_complex", "is_active", "created_at_utc", "updated_at_utc" },
                columnTypes: new[] { "uuid", "text", "text", "text", "boolean", "timestamp with time zone", "timestamp with time zone" },
                values: rows);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            var ids = new object[NewDistricts.Length];
            for (var i = 0; i < ids.Length; i++)
                ids[i] = AreaId(11 + i);

            migrationBuilder.DeleteData(table: "service_areas", keyColumn: "id", keyColumnType: "uuid", keyValues: ids);
            migrationBuilder.Sql($"UPDATE service_areas SET district = 'Quận 7', ward_or_complex = 'Phú Mỹ Hưng', updated_at_utc = now() WHERE id = '{AreaId(3)}';");
            migrationBuilder.Sql($"UPDATE service_areas SET district = 'Bình Thạnh', updated_at_utc = now() WHERE id = '{AreaId(4)}';");
            migrationBuilder.Sql($"UPDATE service_areas SET district = 'Tân Bình', updated_at_utc = now() WHERE id = '{AreaId(5)}';");
            migrationBuilder.Sql("UPDATE service_areas SET is_active = true, updated_at_utc = now() WHERE city <> 'Hồ Chí Minh';");
        }
    }
}
