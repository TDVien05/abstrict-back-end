using Abstrict.Api.Models.Enums;

namespace Abstrict.Api.Models.Entities;

public sealed class FreelancerProfile : Entity
{
    public Guid ProviderId { get; set; }
    public Guid UserId { get; set; }
    public required string LegalFullName { get; set; }
    public DateOnly DateOfBirth { get; set; }
    public Gender Gender { get; set; }
    public required string PermanentAddress { get; set; }
    public required string CurrentAddress { get; set; }
    public int ExperienceYears { get; set; }
    public bool AutoAcceptBookings { get; set; }
    /// <summary>Số CCCD đã mã hóa bằng ASP.NET Data Protection; chỉ admin duyệt KYC mới được giải mã.</summary>
    public string CitizenIdNumberProtected { get; set; } = string.Empty;
    /// <summary>HMAC-SHA256 của số CCCD, dùng để chặn một CCCD đăng ký nhiều tài khoản.</summary>
    public string CitizenIdHash { get; set; } = string.Empty;
    public string CitizenIdLast4 { get; set; } = string.Empty;
    public Provider Provider { get; set; } = null!;
    public User User { get; set; } = null!;
}
