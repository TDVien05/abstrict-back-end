using Abstrict.Api.DTOs.Responses;

namespace Abstrict.Api.Services.Interfaces;

public sealed record AvatarContent(Stream Stream, string ContentType);

public interface ICustomerAvatarService
{
    /// <summary>Tải lên hoặc thay ảnh đại diện; ảnh cũ bị xóa khỏi kho lưu trữ.</summary>
    Task<CustomerAvatarResponse> UploadAsync(Guid userId, long length, Stream content, CancellationToken cancellationToken);

    /// <summary>Mở ảnh đại diện của chính người dùng; <c>null</c> nếu chưa có.</summary>
    Task<AvatarContent?> OpenAsync(Guid userId, CancellationToken cancellationToken);

    Task DeleteAsync(Guid userId, CancellationToken cancellationToken);
}
