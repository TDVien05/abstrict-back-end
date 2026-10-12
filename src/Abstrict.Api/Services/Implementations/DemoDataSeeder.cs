using Abstrict.Api.Data;
using Abstrict.Api.Models.Entities;
using Abstrict.Api.Models.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Abstrict.Api.Services.Implementations;

/// <summary>
/// Tạo dữ liệu demo khi <c>Demo:SeedFreelancers</c> bật: 8 freelancer giả (số 0910000001-0910000008), lịch rảnh 14 ngày
/// cho 5 người đã duyệt và một khách hàng thử (0920000001). Mật khẩu lấy từ <c>Demo:SeedPassword</c> (&gt;= 12 ký tự).
/// Idempotent: ID cố định, bỏ qua bản ghi đã có, và mỗi lần khởi động lại kéo dài lịch rảnh. Cùng dữ liệu với
/// scripts/seed-fake-freelancers.ps1 và scripts/seed-booking-fixtures.ps1.
/// </summary>
public static class DemoDataSeeder
{
    private const int AvailabilityDays = 14;
    private const int ApprovedWithAvailability = 5;
    private const string CustomerPhone = "0920000001";

    private static readonly string[] CategoryCodes =
        ["CLEANING", "DEEP_CLEANING", "COOKING", "LAUNDRY", "CHILDCARE", "ELDERLY_CARE", "AC_SERVICE", "ELECTRICAL", "PLUMBING", "MOVING", "GARDENING", "PEST_CONTROL"];

    private sealed record Person(
        string Name, Gender Gender, string Dob, ApprovalStatus Status, double Rating, int Count, int Done, int Experience,
        string? Bio, (string Category, long Rate)[] Services, int[] Areas, string Address);

    private static readonly Person[] People =
    [
        new("Nguyễn Thị Lan", Gender.Female, "1992-03-14", ApprovalStatus.Approved, 4.8, 42, 38, 6, "Dọn dẹp nhà cửa gọn gàng, tỉ mỉ.", [("CLEANING", 80000), ("DEEP_CLEANING", 120000)], [1, 2, 3], "12 Nguyễn Huệ, Quận 1, TP.HCM"),
        new("Trần Văn Hùng", Gender.Male, "1988-11-02", ApprovalStatus.Approved, 4.6, 25, 22, 9, "Thợ điện nước nhiều năm kinh nghiệm.", [("ELECTRICAL", 150000), ("PLUMBING", 140000)], [3, 4, 5], "45 Phạm Văn Đồng, Bình Thạnh, TP.HCM"),
        new("Lê Thị Mai", Gender.Female, "1995-07-21", ApprovalStatus.Approved, 4.9, 61, 57, 4, "Nấu ăn gia đình, món Bắc - Nam.", [("COOKING", 100000)], [6, 7], "8 Láng Hạ, Đống Đa, Hà Nội"),
        new("Phạm Quốc Bảo", Gender.Male, "1990-01-30", ApprovalStatus.Approved, 4.3, 11, 9, 5, "Vệ sinh và bảo trì máy lạnh.", [("AC_SERVICE", 130000)], [1, 5], "99 Cộng Hòa, Tân Bình, TP.HCM"),
        new("Võ Thị Hồng", Gender.Female, "1985-09-09", ApprovalStatus.Approved, 5.0, 8, 8, 12, "Chăm sóc người cao tuổi, có chứng chỉ điều dưỡng.", [("ELDERLY_CARE", 110000), ("CHILDCARE", 100000)], [9, 10], "3 Bạch Đằng, Hải Châu, Đà Nẵng"),
        new("Đặng Minh Tuấn", Gender.Male, "1999-05-17", ApprovalStatus.Submitted, 0, 0, 0, 1, "Mới tham gia, nhận giặt ủi và dọn dẹp.", [("LAUNDRY", 70000), ("CLEANING", 75000)], [2], "21 Cầu Giấy, Cầu Giấy, Hà Nội"),
        new("Hoàng Thị Thu", Gender.Female, "1993-12-25", ApprovalStatus.UnderReview, 0, 0, 0, 3, "Chăm sóc trẻ em tại nhà.", [("CHILDCARE", 95000)], [7, 8], "56 Xuân Thủy, Cầu Giấy, Hà Nội"),
        new("Bùi Anh Khoa", Gender.Male, "2000-08-08", ApprovalStatus.Draft, 0, 0, 0, 0, null, [], [], "17 Lý Thường Kiệt, Quận 3, TP.HCM")
    ];

    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration, ILogger logger, CancellationToken cancellationToken = default)
    {
        var password = configuration["Demo:SeedPassword"];
        if (string.IsNullOrWhiteSpace(password) || password.Length < 12)
        {
            logger.LogWarning("Demo:SeedFreelancers đang bật nhưng Demo:SeedPassword thiếu hoặc ngắn hơn 12 ký tự; bỏ qua dữ liệu demo.");
            return;
        }

        try
        {
            var dbContext = services.GetRequiredService<AppDbContext>();
            var hasher = services.GetRequiredService<IPasswordHasher<User>>();
            var created = await SeedFreelancersAsync(dbContext, hasher, password, cancellationToken);
            var windows = await SeedAvailabilityAsync(dbContext, cancellationToken);
            var customer = await SeedCustomerAsync(dbContext, hasher, password, cancellationToken);
            logger.LogWarning(
                "Dữ liệu demo đang bật: {Freelancers} freelancer mới, {Windows} khung giờ rảnh mới, khách thử mới: {Customer}. Các tài khoản 0910000001-0910000008 và {CustomerPhone} dùng chung Demo:SeedPassword.",
                created, windows, customer, CustomerPhone);
        }
        catch (Exception exception)
        {
            // Dữ liệu demo chỉ là phụ trợ: lỗi ở đây không được làm app không khởi động được.
            logger.LogError(exception, "Không thể tạo dữ liệu demo.");
        }
    }

    private static async Task<int> SeedFreelancersAsync(AppDbContext dbContext, IPasswordHasher<User> hasher, string password, CancellationToken cancellationToken)
    {
        var phones = People.Select((_, i) => PhoneOf(i + 1)).ToList();
        var takenPhones = await dbContext.Users.Where(x => phones.Contains(x.PhoneNumber)).Select(x => x.PhoneNumber).ToListAsync(cancellationToken);
        var categories = await dbContext.ServiceCategories.ToDictionaryAsync(x => x.Code, x => x.Id, cancellationToken);
        var areaIds = (await dbContext.ServiceAreas.Select(x => x.Id).ToListAsync(cancellationToken)).ToHashSet();

        var created = 0;
        for (var i = 0; i < People.Length; i++)
        {
            var n = i + 1;
            var person = People[i];
            if (takenPhones.Contains(PhoneOf(n)))
                continue;

            var userId = Id("cccccccc-0000-0000-0000", n);
            var providerId = Id("dddddddd-0000-0000-0000", n);
            var draft = person.Status == ApprovalStatus.Draft;
            var cccd = $"0790{90000000 + n:D8}";

            var user = new User
            {
                Id = userId,
                PhoneNumber = PhoneOf(n),
                Role = UserRole.Freelancer,
                Status = draft ? AccountStatus.Pending : AccountStatus.Active,
                PhoneVerifiedAtUtc = draft ? null : DateTimeOffset.UtcNow,
                TermsAcceptedAtUtc = DateTimeOffset.UtcNow,
                PrivacyPolicyAcceptedAtUtc = DateTimeOffset.UtcNow
            };
            user.PasswordHash = hasher.HashPassword(user, password);

            dbContext.Users.Add(user);
            dbContext.Providers.Add(new Provider
            {
                Id = providerId,
                Type = ProviderType.Freelancer,
                DisplayName = person.Name,
                ApprovalStatus = person.Status,
                IsAcceptingBookings = person.Status == ApprovalStatus.Approved,
                Bio = person.Bio,
                AverageRating = person.Rating,
                RatingCount = person.Count,
                CompletedBookingCount = person.Done
            });
            dbContext.FreelancerProfiles.Add(new FreelancerProfile
            {
                Id = Id("eeeeeeee-0000-0000-0000", n),
                ProviderId = providerId,
                UserId = userId,
                LegalFullName = person.Name,
                DateOfBirth = DateOnly.Parse(person.Dob),
                Gender = person.Gender,
                PermanentAddress = person.Address,
                CurrentAddress = person.Address,
                ExperienceYears = person.Experience,
                AutoAcceptBookings = false,
                // Số CCCD giả: không giải mã được, chỉ để thỏa ràng buộc duy nhất.
                CitizenIdHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"fake-{cccd}"))).ToLowerInvariant(),
                CitizenIdLast4 = cccd[^4..],
                CitizenIdNumberProtected = "FAKE-SEED-NOT-DECRYPTABLE"
            });

            var k = 0;
            foreach (var (category, rate) in person.Services)
            {
                if (!categories.TryGetValue(category, out var categoryId))
                    throw new InvalidOperationException($"Thiếu danh mục dịch vụ {category}; cần áp dụng migration trước khi seed dữ liệu demo.");
                dbContext.ProviderServices.Add(new ProviderService
                {
                    Id = new Guid($"ffffffff-0000-0000-{n:D4}-{++k:D12}"),
                    ProviderId = providerId,
                    ServiceCategoryId = categoryId,
                    HourlyRateVnd = rate
                });
            }

            k = 0;
            foreach (var area in person.Areas)
            {
                var areaId = Id("bbbbbbbb-0000-0000-0000", area);
                if (!areaIds.Contains(areaId))
                    throw new InvalidOperationException($"Thiếu khu vực {areaId}; cần áp dụng migration trước khi seed dữ liệu demo.");
                dbContext.ProviderServiceAreas.Add(new ProviderServiceArea
                {
                    Id = new Guid($"99999999-0000-0000-{n:D4}-{++k:D12}"),
                    ProviderId = providerId,
                    ServiceAreaId = areaId,
                    TravelRadiusKm = 10
                });
            }

            created++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return created;
    }

    // Việt Nam UTC+7, không có giờ mùa hè: 08-12h và 13-18h giờ địa phương là 01-05h và 06-11h UTC cùng ngày.
    private static async Task<int> SeedAvailabilityAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        var providerIds = Enumerable.Range(1, ApprovedWithAvailability).Select(n => Id("dddddddd-0000-0000-0000", n)).ToList();
        var existingProviders = await dbContext.Providers.Where(x => providerIds.Contains(x.Id)).Select(x => x.Id).ToListAsync(cancellationToken);
        if (existingProviders.Count == 0)
            return 0;

        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var from = new DateTimeOffset(today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var existing = (await dbContext.AvailabilityWindows
            .Where(x => providerIds.Contains(x.ProviderId) && x.StartsAtUtc >= from)
            .Select(x => x.Id).ToListAsync(cancellationToken)).ToHashSet();

        var shifts = new[] { (Slot: 1, From: 1, To: 5), (Slot: 2, From: 6, To: 11) };
        var added = 0;
        for (var n = 1; n <= ApprovedWithAvailability; n++)
        {
            var providerId = Id("dddddddd-0000-0000-0000", n);
            if (!existingProviders.Contains(providerId))
                continue;

            for (var d = 0; d < AvailabilityDays; d++)
            {
                var day = today.AddDays(d);
                foreach (var shift in shifts)
                {
                    var id = new Guid($"a11a0000-0000-0000-{n:D4}-{day:yyyyMMdd}{shift.Slot:D4}");
                    if (existing.Contains(id))
                        continue;
                    var date = day.ToDateTime(TimeOnly.MinValue);
                    dbContext.AvailabilityWindows.Add(new AvailabilityWindow
                    {
                        Id = id,
                        ProviderId = providerId,
                        StartsAtUtc = new DateTimeOffset(date.AddHours(shift.From), TimeSpan.Zero),
                        EndsAtUtc = new DateTimeOffset(date.AddHours(shift.To), TimeSpan.Zero)
                    });
                    added++;
                }
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return added;
    }

    private static async Task<bool> SeedCustomerAsync(AppDbContext dbContext, IPasswordHasher<User> hasher, string password, CancellationToken cancellationToken)
    {
        if (await dbContext.Users.AnyAsync(x => x.PhoneNumber == CustomerPhone, cancellationToken))
            return false;

        var userId = new Guid("c0c0c0c0-0000-0000-0000-000000000001");
        var now = DateTimeOffset.UtcNow;
        var user = new User
        {
            Id = userId,
            PhoneNumber = CustomerPhone,
            Role = UserRole.Customer,
            Status = AccountStatus.Active,
            PhoneVerifiedAtUtc = now,
            TermsAcceptedAtUtc = now,
            PrivacyPolicyAcceptedAtUtc = now
        };
        user.PasswordHash = hasher.HashPassword(user, password);
        dbContext.Users.Add(user);
        dbContext.CustomerProfiles.Add(new CustomerProfile { Id = new Guid("c0c0c0c0-0000-0000-0001-000000000001"), UserId = userId, FullName = "Khách Hàng Thử" });
        dbContext.CustomerAddresses.Add(new CustomerAddress
        {
            Id = new Guid("c0c0c0c0-0000-0000-0002-000000000001"),
            CustomerUserId = userId,
            Label = "Nhà",
            ApartmentNumber = "A-1205",
            Floor = "12",
            BuildingName = "Vinhomes Golden River",
            StreetAddress = "2 Tôn Đức Thắng",
            Ward = "Bến Nghé",
            District = "Quận 1",
            City = "Hồ Chí Minh",
            AccessInstructions = "Gọi lễ tân",
            IsDefault = true
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static string PhoneOf(int n) => $"09100000{n:D2}";

    private static Guid Id(string prefix, int n) => new($"{prefix}-{n:D12}");
}
