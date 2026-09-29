using System;
using Abstrict.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abstrict.Api.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260929090000_SeedCatalogData")]
    public partial class SeedCatalogData : Migration
    {
        private static readonly DateTimeOffset SeededAt = new(2026, 9, 29, 0, 0, 0, TimeSpan.Zero);

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "service_categories",
                columns: new[] { "id", "code", "name", "description", "is_active", "created_at_utc", "updated_at_utc" },
                values: new object[,]
                {
                    { new Guid("aaaaaaaa-0000-0000-0000-000000000001"), "CLEANING", "Dọn dẹp nhà cửa", "Dọn dẹp, vệ sinh nhà cửa theo giờ hoặc định kỳ.", true, SeededAt, SeededAt },
                    { new Guid("aaaaaaaa-0000-0000-0000-000000000002"), "DEEP_CLEANING", "Vệ sinh chuyên sâu", "Tổng vệ sinh, xử lý vết bẩn cứng đầu, vệ sinh sau xây dựng.", true, SeededAt, SeededAt },
                    { new Guid("aaaaaaaa-0000-0000-0000-000000000003"), "COOKING", "Nấu ăn", "Nấu ăn tại nhà theo thực đơn và khẩu phần yêu cầu.", true, SeededAt, SeededAt },
                    { new Guid("aaaaaaaa-0000-0000-0000-000000000004"), "LAUNDRY", "Giặt ủi", "Giặt, sấy và ủi quần áo, chăn ga gối đệm.", true, SeededAt, SeededAt },
                    { new Guid("aaaaaaaa-0000-0000-0000-000000000005"), "CHILDCARE", "Trông trẻ", "Trông nom, chăm sóc trẻ em tại nhà.", true, SeededAt, SeededAt },
                    { new Guid("aaaaaaaa-0000-0000-0000-000000000006"), "ELDERLY_CARE", "Chăm sóc người cao tuổi", "Hỗ trợ, chăm sóc người cao tuổi tại nhà.", true, SeededAt, SeededAt },
                    { new Guid("aaaaaaaa-0000-0000-0000-000000000007"), "AC_SERVICE", "Vệ sinh & sửa máy lạnh", "Vệ sinh, bảo trì và sửa chữa máy lạnh.", true, SeededAt, SeededAt },
                    { new Guid("aaaaaaaa-0000-0000-0000-000000000008"), "ELECTRICAL", "Sửa chữa điện", "Sửa chữa, lắp đặt hệ thống điện trong nhà.", true, SeededAt, SeededAt },
                    { new Guid("aaaaaaaa-0000-0000-0000-000000000009"), "PLUMBING", "Sửa chữa nước", "Sửa chữa, lắp đặt đường ống nước và thiết bị vệ sinh.", true, SeededAt, SeededAt },
                    { new Guid("aaaaaaaa-0000-0000-0000-000000000010"), "MOVING", "Chuyển nhà", "Vận chuyển, bốc xếp đồ đạc khi chuyển nhà.", true, SeededAt, SeededAt },
                    { new Guid("aaaaaaaa-0000-0000-0000-000000000011"), "GARDENING", "Chăm sóc cây cảnh", "Cắt tỉa, chăm sóc cây cảnh và sân vườn.", true, SeededAt, SeededAt },
                    { new Guid("aaaaaaaa-0000-0000-0000-000000000012"), "PEST_CONTROL", "Diệt côn trùng", "Phun thuốc, diệt côn trùng và mối mọt.", true, SeededAt, SeededAt }
                });

            migrationBuilder.InsertData(
                table: "service_areas",
                columns: new[] { "id", "city", "district", "ward_or_complex", "is_active", "created_at_utc", "updated_at_utc" },
                values: new object[,]
                {
                    { new Guid("bbbbbbbb-0000-0000-0000-000000000001"), "Hồ Chí Minh", "Quận 1", null, true, SeededAt, SeededAt },
                    { new Guid("bbbbbbbb-0000-0000-0000-000000000002"), "Hồ Chí Minh", "Quận 3", null, true, SeededAt, SeededAt },
                    { new Guid("bbbbbbbb-0000-0000-0000-000000000003"), "Hồ Chí Minh", "Quận 7", "Phú Mỹ Hưng", true, SeededAt, SeededAt },
                    { new Guid("bbbbbbbb-0000-0000-0000-000000000004"), "Hồ Chí Minh", "Bình Thạnh", null, true, SeededAt, SeededAt },
                    { new Guid("bbbbbbbb-0000-0000-0000-000000000005"), "Hồ Chí Minh", "Tân Bình", null, true, SeededAt, SeededAt },
                    { new Guid("bbbbbbbb-0000-0000-0000-000000000006"), "Hà Nội", "Hoàn Kiếm", null, true, SeededAt, SeededAt },
                    { new Guid("bbbbbbbb-0000-0000-0000-000000000007"), "Hà Nội", "Cầu Giấy", null, true, SeededAt, SeededAt },
                    { new Guid("bbbbbbbb-0000-0000-0000-000000000008"), "Hà Nội", "Đống Đa", null, true, SeededAt, SeededAt },
                    { new Guid("bbbbbbbb-0000-0000-0000-000000000009"), "Đà Nẵng", "Hải Châu", null, true, SeededAt, SeededAt },
                    { new Guid("bbbbbbbb-0000-0000-0000-000000000010"), "Đà Nẵng", "Sơn Trà", null, true, SeededAt, SeededAt }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "service_areas",
                keyColumn: "id",
                keyValues: new object[]
                {
                    new Guid("bbbbbbbb-0000-0000-0000-000000000001"),
                    new Guid("bbbbbbbb-0000-0000-0000-000000000002"),
                    new Guid("bbbbbbbb-0000-0000-0000-000000000003"),
                    new Guid("bbbbbbbb-0000-0000-0000-000000000004"),
                    new Guid("bbbbbbbb-0000-0000-0000-000000000005"),
                    new Guid("bbbbbbbb-0000-0000-0000-000000000006"),
                    new Guid("bbbbbbbb-0000-0000-0000-000000000007"),
                    new Guid("bbbbbbbb-0000-0000-0000-000000000008"),
                    new Guid("bbbbbbbb-0000-0000-0000-000000000009"),
                    new Guid("bbbbbbbb-0000-0000-0000-000000000010")
                });

            migrationBuilder.DeleteData(
                table: "service_categories",
                keyColumn: "id",
                keyValues: new object[]
                {
                    new Guid("aaaaaaaa-0000-0000-0000-000000000001"),
                    new Guid("aaaaaaaa-0000-0000-0000-000000000002"),
                    new Guid("aaaaaaaa-0000-0000-0000-000000000003"),
                    new Guid("aaaaaaaa-0000-0000-0000-000000000004"),
                    new Guid("aaaaaaaa-0000-0000-0000-000000000005"),
                    new Guid("aaaaaaaa-0000-0000-0000-000000000006"),
                    new Guid("aaaaaaaa-0000-0000-0000-000000000007"),
                    new Guid("aaaaaaaa-0000-0000-0000-000000000008"),
                    new Guid("aaaaaaaa-0000-0000-0000-000000000009"),
                    new Guid("aaaaaaaa-0000-0000-0000-000000000010"),
                    new Guid("aaaaaaaa-0000-0000-0000-000000000011"),
                    new Guid("aaaaaaaa-0000-0000-0000-000000000012")
                });
        }
    }
}
