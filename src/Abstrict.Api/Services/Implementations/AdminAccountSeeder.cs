using Abstrict.Api.Data;
using Abstrict.Api.Models.Entities;
using Abstrict.Api.Models.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Abstrict.Api.Services.Implementations;

/// <summary>Tạo tài khoản admin đầu tiên; chỉ đồng bộ lại mật khẩu tài khoản này khi Admin:ResetSeedPasswordOnStartup bật.</summary>
public static class AdminAccountSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration, ILogger logger, CancellationToken cancellationToken = default)
    {
        var rawPhone = configuration["Admin:SeedPhoneNumber"];
        var password = configuration["Admin:SeedPassword"];
        if (string.IsNullOrWhiteSpace(rawPhone) || string.IsNullOrWhiteSpace(password))
            return;

        var phone = VietnamesePhoneNumber.Normalize(rawPhone)
            ?? throw new InvalidOperationException("Admin:SeedPhoneNumber không phải số điện thoại Việt Nam hợp lệ.");
        if (password.Length < 12)
            throw new InvalidOperationException("Admin:SeedPassword cần có ít nhất 12 ký tự.");

        var dbContext = services.GetRequiredService<AppDbContext>();
        var hasher = services.GetRequiredService<IPasswordHasher<User>>();
        var existing = await dbContext.Users.SingleOrDefaultAsync(x => x.PhoneNumber == phone, cancellationToken);
        if (existing is not null)
        {
            if (!configuration.GetValue<bool>("Admin:ResetSeedPasswordOnStartup") || existing.Role != UserRole.Admin)
                return;

            existing.PasswordHash = hasher.HashPassword(existing, password);
            existing.Status = AccountStatus.Active;
            existing.PhoneVerifiedAtUtc ??= DateTimeOffset.UtcNow;
            existing.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogWarning("Đã đồng bộ mật khẩu tài khoản admin cấu hình cho số {Phone} theo cờ Admin:ResetSeedPasswordOnStartup.", phone);
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var user = new User
        {
            PhoneNumber = phone,
            Role = UserRole.Admin,
            Status = AccountStatus.Active,
            PhoneVerifiedAtUtc = now
        };
        user.PasswordHash = hasher.HashPassword(user, password);
        user.CustomerProfile = new CustomerProfile { User = user, FullName = configuration["Admin:SeedFullName"] ?? "Quản trị viên" };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Đã tạo tài khoản admin ban đầu cho số {Phone}.", phone);
    }
}
