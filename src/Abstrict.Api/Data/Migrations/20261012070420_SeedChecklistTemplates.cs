using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abstrict.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedChecklistTemplates : Migration
    {
        private static readonly DateTimeOffset SeededAt = new(2026, 10, 12, 0, 0, 0, TimeSpan.Zero);

        private static readonly Guid Cleaning = new("c0000000-0000-0000-0000-000000000001");
        private static readonly Guid DeepCleaning = new("c0000000-0000-0000-0000-000000000002");
        private static readonly Guid Cooking = new("c0000000-0000-0000-0000-000000000003");
        private static readonly Guid Laundry = new("c0000000-0000-0000-0000-000000000004");

        private static Guid Item(int template, int item) => new($"c1000000-0000-0000-{template:D4}-{item:D12}");

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "checklist_templates",
                columns: new[] { "id", "service_category_id", "name", "version", "is_active", "created_at_utc", "updated_at_utc" },
                columnTypes: new[] { "uuid", "uuid", "text", "integer", "boolean", "timestamp with time zone", "timestamp with time zone" },
                values: new object[,]
                {
                    { Cleaning, new Guid("aaaaaaaa-0000-0000-0000-000000000001"), "Dọn dẹp nhà cửa cơ bản", 1, true, SeededAt, SeededAt },
                    { DeepCleaning, new Guid("aaaaaaaa-0000-0000-0000-000000000002"), "Vệ sinh chuyên sâu", 1, true, SeededAt, SeededAt },
                    { Cooking, new Guid("aaaaaaaa-0000-0000-0000-000000000003"), "Nấu ăn tại nhà", 1, true, SeededAt, SeededAt },
                    { Laundry, new Guid("aaaaaaaa-0000-0000-0000-000000000004"), "Giặt ủi", 1, true, SeededAt, SeededAt }
                });

            migrationBuilder.InsertData(
                table: "checklist_template_items",
                columns: new[] { "id", "checklist_template_id", "parent_item_id", "area_name", "description", "sort_order", "is_required", "created_at_utc", "updated_at_utc" },
                columnTypes: new[] { "uuid", "uuid", "uuid", "text", "text", "integer", "boolean", "timestamp with time zone", "timestamp with time zone" },
                values: new object[,]
                {
                    { Item(1, 1), Cleaning, null, "Phòng khách", "Quét và lau sàn", 1, true, SeededAt, SeededAt },
                    { Item(1, 2), Cleaning, null, "Phòng khách", "Lau bụi bàn ghế, kệ tủ", 2, true, SeededAt, SeededAt },
                    { Item(1, 3), Cleaning, null, "Phòng ngủ", "Quét, lau sàn và sắp xếp giường", 3, false, SeededAt, SeededAt },
                    { Item(1, 4), Cleaning, null, "Bếp", "Lau mặt bếp, bồn rửa và rửa chén bát", 4, false, SeededAt, SeededAt },
                    { Item(1, 5), Cleaning, null, "Nhà vệ sinh", "Cọ rửa bồn cầu, lavabo và sàn", 5, true, SeededAt, SeededAt },
                    { Item(1, 6), Cleaning, null, "Ban công", "Quét dọn ban công", 6, false, SeededAt, SeededAt },
                    { Item(1, 7), Cleaning, null, "Chung", "Thu gom và đổ rác", 7, false, SeededAt, SeededAt },

                    { Item(2, 1), DeepCleaning, null, "Toàn nhà", "Hút bụi, lau sàn kỹ từng phòng", 1, true, SeededAt, SeededAt },
                    { Item(2, 2), DeepCleaning, null, "Toàn nhà", "Lau cửa kính, cửa sổ và khung cửa", 2, true, SeededAt, SeededAt },
                    { Item(2, 3), DeepCleaning, null, "Bếp", "Tẩy dầu mỡ bếp, máy hút mùi và tường bếp", 3, true, SeededAt, SeededAt },
                    { Item(2, 4), DeepCleaning, null, "Bếp", "Vệ sinh bên trong tủ lạnh, lò vi sóng", 4, false, SeededAt, SeededAt },
                    { Item(2, 5), DeepCleaning, null, "Nhà vệ sinh", "Tẩy cặn, ố vàng thiết bị vệ sinh", 5, false, SeededAt, SeededAt },
                    { Item(2, 6), DeepCleaning, null, "Phòng ngủ", "Hút bụi nệm, rèm và gầm giường", 6, false, SeededAt, SeededAt },
                    { Item(2, 7), DeepCleaning, null, "Toàn nhà", "Lau quạt trần, đèn và công tắc", 7, false, SeededAt, SeededAt },

                    { Item(3, 1), Cooking, null, "Chuẩn bị", "Sơ chế nguyên liệu theo thực đơn", 1, true, SeededAt, SeededAt },
                    { Item(3, 2), Cooking, null, "Nấu ăn", "Nấu các món theo thực đơn và khẩu phần đã thống nhất", 2, true, SeededAt, SeededAt },
                    { Item(3, 3), Cooking, null, "Chuẩn bị", "Đi chợ mua nguyên liệu (chi phí nguyên liệu khách trả riêng)", 3, false, SeededAt, SeededAt },
                    { Item(3, 4), Cooking, null, "Sau bữa ăn", "Rửa chén bát và dụng cụ nấu", 4, false, SeededAt, SeededAt },
                    { Item(3, 5), Cooking, null, "Sau bữa ăn", "Lau dọn khu vực bếp", 5, true, SeededAt, SeededAt },

                    { Item(4, 1), Laundry, null, "Giặt", "Phân loại quần áo và giặt máy", 1, true, SeededAt, SeededAt },
                    { Item(4, 2), Laundry, null, "Giặt", "Phơi hoặc sấy quần áo", 2, true, SeededAt, SeededAt },
                    { Item(4, 3), Laundry, null, "Giặt", "Giặt tay đồ dễ hỏng", 3, false, SeededAt, SeededAt },
                    { Item(4, 4), Laundry, null, "Ủi", "Ủi quần áo", 4, false, SeededAt, SeededAt },
                    { Item(4, 5), Laundry, null, "Sắp xếp", "Gấp và xếp quần áo vào tủ", 5, false, SeededAt, SeededAt },
                    { Item(4, 6), Laundry, null, "Giặt", "Giặt chăn ga gối", 6, false, SeededAt, SeededAt }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM checklist_template_items WHERE checklist_template_id IN (SELECT id FROM checklist_templates WHERE id::text LIKE 'c0000000-0000-0000-0000-00000000000_');");
            migrationBuilder.DeleteData(
                table: "checklist_templates",
                keyColumn: "id",
                keyColumnType: "uuid",
                keyValues: new object[] { Cleaning, DeepCleaning, Cooking, Laundry });
        }
    }
}
