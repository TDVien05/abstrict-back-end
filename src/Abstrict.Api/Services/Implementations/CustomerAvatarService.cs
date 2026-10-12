using Abstrict.Api.Common;
using Abstrict.Api.Data;
using Abstrict.Api.DTOs.Responses;
using Abstrict.Api.Integrations.Storage;
using Abstrict.Api.Models.Entities;
using Abstrict.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Abstrict.Api.Services.Implementations;

public sealed class CustomerAvatarService(AppDbContext dbContext, IFileStorage fileStorage, TimeProvider timeProvider) : ICustomerAvatarService
{
    public const long MaximumBytes = 5 * 1024 * 1024;
    public const string AvatarRoute = "/" + ApiRoutes.Version1 + "/customer/profile/avatar";

    public async Task<CustomerAvatarResponse> UploadAsync(Guid userId, long length, Stream content, CancellationToken cancellationToken)
    {
        if (length <= 0)
            throw new BookingFlowException(StatusCodes.Status400BadRequest, "EMPTY_FILE", "Tệp tải lên đang trống.");
        if (length > MaximumBytes)
            throw new BookingFlowException(StatusCodes.Status413PayloadTooLarge, "FILE_TOO_LARGE", "Ảnh không được vượt quá 5 MB.");

        var profile = await LoadProfileAsync(userId, cancellationToken);

        // Tin nội dung thực của file, không tin Content-Type hay đuôi file do client gửi.
        var header = new byte[8];
        var read = await content.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, cancellationToken);
        var image = KycImage.Detect(header.AsSpan(0, read))
            ?? throw new BookingFlowException(StatusCodes.Status415UnsupportedMediaType, "UNSUPPORTED_IMAGE", "Chỉ chấp nhận ảnh JPG hoặc PNG.");
        content.Position = 0;

        var previousKey = profile.AvatarObjectKey;
        var newKey = await fileStorage.SaveAsync($"avatars/customers/{userId:N}", image.Extension, content, cancellationToken);
        profile.AvatarObjectKey = newKey;
        profile.UpdatedAtUtc = timeProvider.GetUtcNow();

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await fileStorage.DeleteAsync(newKey, CancellationToken.None);
            throw;
        }

        if (previousKey is not null)
            await fileStorage.DeleteAsync(previousKey, CancellationToken.None);
        return ToResponse(profile);
    }

    public async Task<AvatarContent?> OpenAsync(Guid userId, CancellationToken cancellationToken)
    {
        var key = await dbContext.CustomerProfiles.AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => x.AvatarObjectKey)
            .SingleOrDefaultAsync(cancellationToken);
        if (key is null)
            return null;

        var stream = await fileStorage.OpenReadAsync(key, cancellationToken);
        return stream is null ? null : new AvatarContent(stream, KycImage.ContentTypeFromKey(key));
    }

    public async Task DeleteAsync(Guid userId, CancellationToken cancellationToken)
    {
        var profile = await LoadProfileAsync(userId, cancellationToken);
        if (profile.AvatarObjectKey is not { } key)
            return;

        profile.AvatarObjectKey = null;
        profile.UpdatedAtUtc = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);
        await fileStorage.DeleteAsync(key, CancellationToken.None);
    }

    private async Task<CustomerProfile> LoadProfileAsync(Guid userId, CancellationToken cancellationToken) =>
        await dbContext.CustomerProfiles.SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken)
        ?? throw new BookingFlowException(StatusCodes.Status404NotFound, "CUSTOMER_PROFILE_NOT_FOUND", "Không tìm thấy hồ sơ khách hàng.");

    // Tham số v đổi theo mỗi lần cập nhật để trình duyệt không dùng lại ảnh cũ.
    public static string? BuildAvatarUrl(CustomerProfile profile) =>
        profile.AvatarObjectKey is null ? null : $"{AvatarRoute}?v={profile.UpdatedAtUtc.ToUnixTimeMilliseconds()}";

    public static CustomerProfileResponse ToProfileResponse(User user, CustomerProfile profile) =>
        new(user.Id, profile.FullName, user.PhoneNumber, BuildAvatarUrl(profile), profile.CreatedAtUtc);

    private static CustomerAvatarResponse ToResponse(CustomerProfile profile) =>
        new(BuildAvatarUrl(profile)!, profile.UpdatedAtUtc);
}
