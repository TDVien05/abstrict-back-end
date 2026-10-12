namespace Abstrict.Api.DTOs.Responses;

/// <summary><c>AvatarUrl</c> là đường dẫn tương đối, cần gửi kèm access token khi tải ảnh.</summary>
public sealed record CustomerAvatarResponse(string AvatarUrl, DateTimeOffset UpdatedAtUtc);

/// <summary>Hồ sơ khách hàng để FE hiển thị; <c>AvatarUrl</c> là <c>null</c> khi chưa có ảnh đại diện.</summary>
public sealed record CustomerProfileResponse(
    Guid UserId,
    string FullName,
    string PhoneNumber,
    string? AvatarUrl,
    DateTimeOffset CreatedAtUtc);
