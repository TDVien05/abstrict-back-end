using Abstrict.Api.Data;
using Abstrict.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Abstrict.Api.Services.Implementations;

/// <summary>Seed dữ liệu danh mục (service categories, service areas, checklist templates) nếu chưa có.</summary>
public static class CatalogDataSeeder
{
    private static readonly DateTimeOffset SeededAt = new(2026, 9, 29, 0, 0, 0, TimeSpan.Zero);

    public static async Task SeedAsync(IServiceProvider services, ILogger logger, CancellationToken cancellationToken = default)
    {
        var dbContext = services.GetRequiredService<AppDbContext>();

        // Seed Service Categories
        if (!await dbContext.ServiceCategories.AnyAsync(cancellationToken))
        {
            logger.LogInformation("Seeding service categories...");
            var categories = new[]
            {
                new ServiceCategory { Id = new Guid("aaaaaaaa-0000-0000-0000-000000000001"), Code = "CLEANING", Name = "Dọn dẹp nhà cửa", Description = "Dọn dẹp, vệ sinh nhà cửa theo giờ hoặc định kỳ.", IsActive = true, CreatedAtUtc = SeededAt, UpdatedAtUtc = SeededAt },
                new ServiceCategory { Id = new Guid("aaaaaaaa-0000-0000-0000-000000000002"), Code = "DEEP_CLEANING", Name = "Vệ sinh chuyên sâu", Description = "Tổng vệ sinh, xử lý vết bẩn cứng đầu, vệ sinh sau xây dựng.", IsActive = true, CreatedAtUtc = SeededAt, UpdatedAtUtc = SeededAt },
                new ServiceCategory { Id = new Guid("aaaaaaaa-0000-0000-0000-000000000003"), Code = "COOKING", Name = "Nấu ăn", Description = "Nấu ăn tại nhà theo thực đơn và khẩu phần yêu cầu.", IsActive = true, CreatedAtUtc = SeededAt, UpdatedAtUtc = SeededAt },
                new ServiceCategory { Id = new Guid("aaaaaaaa-0000-0000-0000-000000000004"), Code = "LAUNDRY", Name = "Giặt ủi", Description = "Giặt, sấy và ủi quần áo, chăn ga gối đệm.", IsActive = true, CreatedAtUtc = SeededAt, UpdatedAtUtc = SeededAt },
                new ServiceCategory { Id = new Guid("aaaaaaaa-0000-0000-0000-000000000005"), Code = "CHILDCARE", Name = "Trông trẻ", Description = "Trông nom, chăm sóc trẻ em tại nhà.", IsActive = true, CreatedAtUtc = SeededAt, UpdatedAtUtc = SeededAt },
                new ServiceCategory { Id = new Guid("aaaaaaaa-0000-0000-0000-000000000006"), Code = "ELDERLY_CARE", Name = "Chăm sóc người cao tuổi", Description = "Hỗ trợ, chăm sóc người cao tuổi tại nhà.", IsActive = true, CreatedAtUtc = SeededAt, UpdatedAtUtc = SeededAt },
                new ServiceCategory { Id = new Guid("aaaaaaaa-0000-0000-0000-000000000007"), Code = "AC_SERVICE", Name = "Vệ sinh & sửa máy lạnh", Description = "Vệ sinh, bảo trì và sửa chữa máy lạnh.", IsActive = true, CreatedAtUtc = SeededAt, UpdatedAtUtc = SeededAt },
                new ServiceCategory { Id = new Guid("aaaaaaaa-0000-0000-0000-000000000008"), Code = "ELECTRICAL", Name = "Sửa chữa điện", Description = "Sửa chữa, lắp đặt hệ thống điện trong nhà.", IsActive = true, CreatedAtUtc = SeededAt, UpdatedAtUtc = SeededAt },
                new ServiceCategory { Id = new Guid("aaaaaaaa-0000-0000-0000-000000000009"), Code = "PLUMBING", Name = "Sửa chữa nước", Description = "Sửa chữa, lắp đặt đường ống nước và thiết bị vệ sinh.", IsActive = true, CreatedAtUtc = SeededAt, UpdatedAtUtc = SeededAt },
                new ServiceCategory { Id = new Guid("aaaaaaaa-0000-0000-0000-000000000010"), Code = "MOVING", Name = "Chuyển nhà", Description = "Vận chuyển, bốc xếp đồ đạc khi chuyển nhà.", IsActive = true, CreatedAtUtc = SeededAt, UpdatedAtUtc = SeededAt },
                new ServiceCategory { Id = new Guid("aaaaaaaa-0000-0000-0000-000000000011"), Code = "GARDENING", Name = "Chăm sóc cây cảnh", Description = "Cắt tỉa, chăm sóc cây cảnh và sân vườn.", IsActive = true, CreatedAtUtc = SeededAt, UpdatedAtUtc = SeededAt },
                new ServiceCategory { Id = new Guid("aaaaaaaa-0000-0000-0000-000000000012"), Code = "PEST_CONTROL", Name = "Diệt côn trùng", Description = "Phun thuốc, diệt côn trùng và mối mọt.", IsActive = true, CreatedAtUtc = SeededAt, UpdatedAtUtc = SeededAt },
            };
            dbContext.ServiceCategories.AddRange(categories);
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded {Count} service categories.", categories.Length);
        }

        // Seed Service Areas
        if (!await dbContext.ServiceAreas.AnyAsync(cancellationToken))
        {
            logger.LogInformation("Seeding service areas...");
            var areas = new[]
            {
                new ServiceArea { Id = new Guid("bbbbbbbb-0000-0000-0000-000000000001"), City = "Hồ Chí Minh", District = "Quận 1", WardOrComplex = null, IsActive = true, CreatedAtUtc = SeededAt, UpdatedAtUtc = SeededAt },
                new ServiceArea { Id = new Guid("bbbbbbbb-0000-0000-0000-000000000002"), City = "Hồ Chí Minh", District = "Quận 3", WardOrComplex = null, IsActive = true, CreatedAtUtc = SeededAt, UpdatedAtUtc = SeededAt },
                new ServiceArea { Id = new Guid("bbbbbbbb-0000-0000-0000-000000000003"), City = "Hồ Chí Minh", District = "Quận 7", WardOrComplex = "Phú Mỹ Hưng", IsActive = true, CreatedAtUtc = SeededAt, UpdatedAtUtc = SeededAt },
                new ServiceArea { Id = new Guid("bbbbbbbb-0000-0000-0000-000000000004"), City = "Hồ Chí Minh", District = "Bình Thạnh", WardOrComplex = null, IsActive = true, CreatedAtUtc = SeededAt, UpdatedAtUtc = SeededAt },
                new ServiceArea { Id = new Guid("bbbbbbbb-0000-0000-0000-000000000005"), City = "Hồ Chí Minh", District = "Tân Bình", WardOrComplex = null, IsActive = true, CreatedAtUtc = SeededAt, UpdatedAtUtc = SeededAt },
                new ServiceArea { Id = new Guid("bbbbbbbb-0000-0000-0000-000000000006"), City = "Hà Nội", District = "Hoàn Kiếm", WardOrComplex = null, IsActive = true, CreatedAtUtc = SeededAt, UpdatedAtUtc = SeededAt },
                new ServiceArea { Id = new Guid("bbbbbbbb-0000-0000-0000-000000000007"), City = "Hà Nội", District = "Cầu Giấy", WardOrComplex = null, IsActive = true, CreatedAtUtc = SeededAt, UpdatedAtUtc = SeededAt },
                new ServiceArea { Id = new Guid("bbbbbbbb-0000-0000-0000-000000000008"), City = "Hà Nội", District = "Đống Đa", WardOrComplex = null, IsActive = true, CreatedAtUtc = SeededAt, UpdatedAtUtc = SeededAt },
                new ServiceArea { Id = new Guid("bbbbbbbb-0000-0000-0000-000000000009"), City = "Đà Nẵng", District = "Hải Châu", WardOrComplex = null, IsActive = true, CreatedAtUtc = SeededAt, UpdatedAtUtc = SeededAt },
                new ServiceArea { Id = new Guid("bbbbbbbb-0000-0000-0000-000000000010"), City = "Đà Nẵng", District = "Sơn Trà", WardOrComplex = null, IsActive = true, CreatedAtUtc = SeededAt, UpdatedAtUtc = SeededAt },
            };
            dbContext.ServiceAreas.AddRange(areas);
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded {Count} service areas.", areas.Length);
        }

        // Seed Checklist Templates
        if (!await dbContext.ChecklistTemplates.AnyAsync(cancellationToken))
        {
            logger.LogInformation("Seeding checklist templates...");
            var checklistSeededAt = new DateTimeOffset(2026, 10, 12, 0, 0, 0, TimeSpan.Zero);
            var cleaning = new Guid("c0000000-0000-0000-0000-000000000001");
            var deepCleaning = new Guid("c0000000-0000-0000-0000-000000000002");
            var cooking = new Guid("c0000000-0000-0000-0000-000000000003");
            var laundry = new Guid("c0000000-0000-0000-0000-000000000004");

            var templates = new[]
            {
                new ChecklistTemplate { Id = cleaning, ServiceCategoryId = new Guid("aaaaaaaa-0000-0000-0000-000000000001"), Name = "Dọn dẹp nhà cửa cơ bản", Version = 1, IsActive = true, CreatedAtUtc = checklistSeededAt, UpdatedAtUtc = checklistSeededAt },
                new ChecklistTemplate { Id = deepCleaning, ServiceCategoryId = new Guid("aaaaaaaa-0000-0000-0000-000000000002"), Name = "Vệ sinh chuyên sâu", Version = 1, IsActive = true, CreatedAtUtc = checklistSeededAt, UpdatedAtUtc = checklistSeededAt },
                new ChecklistTemplate { Id = cooking, ServiceCategoryId = new Guid("aaaaaaaa-0000-0000-0000-000000000003"), Name = "Nấu ăn tại nhà", Version = 1, IsActive = true, CreatedAtUtc = checklistSeededAt, UpdatedAtUtc = checklistSeededAt },
                new ChecklistTemplate { Id = laundry, ServiceCategoryId = new Guid("aaaaaaaa-0000-0000-0000-000000000004"), Name = "Giặt ủi", Version = 1, IsActive = true, CreatedAtUtc = checklistSeededAt, UpdatedAtUtc = checklistSeededAt },
            };
            dbContext.ChecklistTemplates.AddRange(templates);
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded {Count} checklist templates.", templates.Length);

            // Seed Checklist Template Items
            var items = new List<ChecklistTemplateItem>
            {
                // Cleaning items
                new() { Id = Item(1, 1), ChecklistTemplateId = cleaning, ParentItemId = null, AreaName = "Phòng khách", Description = "Quét và lau sàn", SortOrder = 1, IsRequired = true, CreatedAtUtc = checklistSeededAt, UpdatedAtUtc = checklistSeededAt },
                new() { Id = Item(1, 2), ChecklistTemplateId = cleaning, ParentItemId = null, AreaName = "Phòng khách", Description = "Lau bụi bàn ghế, kệ tủ", SortOrder = 2, IsRequired = true, CreatedAtUtc = checklistSeededAt, UpdatedAtUtc = checklistSeededAt },
                new() { Id = Item(1, 3), ChecklistTemplateId = cleaning, ParentItemId = null, AreaName = "Phòng ngủ", Description = "Quét, lau sàn và sắp xếp giường", SortOrder = 3, IsRequired = false, CreatedAtUtc = checklistSeededAt, UpdatedAtUtc = checklistSeededAt },
                new() { Id = Item(1, 4), ChecklistTemplateId = cleaning, ParentItemId = null, AreaName = "Bếp", Description = "Lau mặt bếp, bồn rửa và rửa chén bát", SortOrder = 4, IsRequired = false, CreatedAtUtc = checklistSeededAt, UpdatedAtUtc = checklistSeededAt },
                new() { Id = Item(1, 5), ChecklistTemplateId = cleaning, ParentItemId = null, AreaName = "Nhà vệ sinh", Description = "Cọ rửa bồn cầu, lavabo và sàn", SortOrder = 5, IsRequired = true, CreatedAtUtc = checklistSeededAt, UpdatedAtUtc = checklistSeededAt },
                new() { Id = Item(1, 6), ChecklistTemplateId = cleaning, ParentItemId = null, AreaName = "Ban công", Description = "Quét dọn ban công", SortOrder = 6, IsRequired = false, CreatedAtUtc = checklistSeededAt, UpdatedAtUtc = checklistSeededAt },
                new() { Id = Item(1, 7), ChecklistTemplateId = cleaning, ParentItemId = null, AreaName = "Chung", Description = "Thu gom và đổ rác", SortOrder = 7, IsRequired = false, CreatedAtUtc = checklistSeededAt, UpdatedAtUtc = checklistSeededAt },

                // Deep Cleaning items
                new() { Id = Item(2, 1), ChecklistTemplateId = deepCleaning, ParentItemId = null, AreaName = "Toàn nhà", Description = "Hút bụi, lau sàn kỹ từng phòng", SortOrder = 1, IsRequired = true, CreatedAtUtc = checklistSeededAt, UpdatedAtUtc = checklistSeededAt },
                new() { Id = Item(2, 2), ChecklistTemplateId = deepCleaning, ParentItemId = null, AreaName = "Toàn nhà", Description = "Lau cửa kính, cửa sổ và khung cửa", SortOrder = 2, IsRequired = true, CreatedAtUtc = checklistSeededAt, UpdatedAtUtc = checklistSeededAt },
                new() { Id = Item(2, 3), ChecklistTemplateId = deepCleaning, ParentItemId = null, AreaName = "Bếp", Description = "Tẩy dầu mỡ bếp, máy hút mùi và tường bếp", SortOrder = 3, IsRequired = true, CreatedAtUtc = checklistSeededAt, UpdatedAtUtc = checklistSeededAt },
                new() { Id = Item(2, 4), ChecklistTemplateId = deepCleaning, ParentItemId = null, AreaName = "Bếp", Description = "Vệ sinh bên trong tủ lạnh, lò vi sóng", SortOrder = 4, IsRequired = false, CreatedAtUtc = checklistSeededAt, UpdatedAtUtc = checklistSeededAt },
                new() { Id = Item(2, 5), ChecklistTemplateId = deepCleaning, ParentItemId = null, AreaName = "Nhà vệ sinh", Description = "Tẩy cặn, ố vàng thiết bị vệ sinh", SortOrder = 5, IsRequired = false, CreatedAtUtc = checklistSeededAt, UpdatedAtUtc = checklistSeededAt },
                new() { Id = Item(2, 6), ChecklistTemplateId = deepCleaning, ParentItemId = null, AreaName = "Phòng ngủ", Description = "Hút bụi nệm, rèm và gầm giường", SortOrder = 6, IsRequired = false, CreatedAtUtc = checklistSeededAt, UpdatedAtUtc = checklistSeededAt },
                new() { Id = Item(2, 7), ChecklistTemplateId = deepCleaning, ParentItemId = null, AreaName = "Toàn nhà", Description = "Lau quạt trần, đèn và công tắc", SortOrder = 7, IsRequired = false, CreatedAtUtc = checklistSeededAt, UpdatedAtUtc = checklistSeededAt },

                // Cooking items
                new() { Id = Item(3, 1), ChecklistTemplateId = cooking, ParentItemId = null, AreaName = "Chuẩn bị", Description = "Sơ chế nguyên liệu theo thực đơn", SortOrder = 1, IsRequired = true, CreatedAtUtc = checklistSeededAt, UpdatedAtUtc = checklistSeededAt },
                new() { Id = Item(3, 2), ChecklistTemplateId = cooking, ParentItemId = null, AreaName = "Nấu ăn", Description = "Nấu các món theo thực đơn và khẩu phần đã thống nhất", SortOrder = 2, IsRequired = true, CreatedAtUtc = checklistSeededAt, UpdatedAtUtc = checklistSeededAt },
                new() { Id = Item(3, 3), ChecklistTemplateId = cooking, ParentItemId = null, AreaName = "Chuẩn bị", Description = "Đi chợ mua nguyên liệu (chi phí nguyên liệu khách trả riêng)", SortOrder = 3, IsRequired = false, CreatedAtUtc = checklistSeededAt, UpdatedAtUtc = checklistSeededAt },
                new() { Id = Item(3, 4), ChecklistTemplateId = cooking, ParentItemId = null, AreaName = "Sau bữa ăn", Description = "Rửa chén bát và dụng cụ nấu", SortOrder = 4, IsRequired = false, CreatedAtUtc = checklistSeededAt, UpdatedAtUtc = checklistSeededAt },
                new() { Id = Item(3, 5), ChecklistTemplateId = cooking, ParentItemId = null, AreaName = "Sau bữa ăn", Description = "Lau dọn khu vực bếp", SortOrder = 5, IsRequired = true, CreatedAtUtc = checklistSeededAt, UpdatedAtUtc = checklistSeededAt },

                // Laundry items
                new() { Id = Item(4, 1), ChecklistTemplateId = laundry, ParentItemId = null, AreaName = "Giặt", Description = "Phân loại quần áo và giặt máy", SortOrder = 1, IsRequired = true, CreatedAtUtc = checklistSeededAt, UpdatedAtUtc = checklistSeededAt },
                new() { Id = Item(4, 2), ChecklistTemplateId = laundry, ParentItemId = null, AreaName = "Giặt", Description = "Phơi hoặc sấy quần áo", SortOrder = 2, IsRequired = true, CreatedAtUtc = checklistSeededAt, UpdatedAtUtc = checklistSeededAt },
                new() { Id = Item(4, 3), ChecklistTemplateId = laundry, ParentItemId = null, AreaName = "Giặt", Description = "Giặt tay đồ dễ hỏng", SortOrder = 3, IsRequired = false, CreatedAtUtc = checklistSeededAt, UpdatedAtUtc = checklistSeededAt },
                new() { Id = Item(4, 4), ChecklistTemplateId = laundry, ParentItemId = null, AreaName = "Ủi", Description = "Ủi quần áo", SortOrder = 4, IsRequired = false, CreatedAtUtc = checklistSeededAt, UpdatedAtUtc = checklistSeededAt },
                new() { Id = Item(4, 5), ChecklistTemplateId = laundry, ParentItemId = null, AreaName = "Sắp xếp", Description = "Gấp và xếp quần áo vào tủ", SortOrder = 5, IsRequired = false, CreatedAtUtc = checklistSeededAt, UpdatedAtUtc = checklistSeededAt },
                new() { Id = Item(4, 6), ChecklistTemplateId = laundry, ParentItemId = null, AreaName = "Giặt", Description = "Giặt chăn ga gối", SortOrder = 6, IsRequired = false, CreatedAtUtc = checklistSeededAt, UpdatedAtUtc = checklistSeededAt },
            };
            dbContext.ChecklistTemplateItems.AddRange(items);
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded {Count} checklist template items.", items.Count);
        }

        logger.LogInformation("Catalog data seeding completed.");
    }

    private static Guid Item(int template, int item) => new($"c1000000-0000-0000-{template:D4}-{item:D12}");
}
