using Abstrict.Api.Data;
using Abstrict.Api.Models.Entities;
using Abstrict.Api.Models.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Abstrict.Api.Services.Implementations;

/// <summary>Tạo tài khoản admin đầu tiên từ cấu hình (Admin:SeedPhoneNumber / Admin:SeedPassword / Admin:SeedFullName); bỏ qua nếu thiếu cấu hình hoặc đã tồn tại.</summary>
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
        if (await dbContext.Users.AnyAsync(x => x.PhoneNumber == phone, cancellationToken))
            return;

        var hasher = services.GetRequiredService<IPasswordHasher<User>>();
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
