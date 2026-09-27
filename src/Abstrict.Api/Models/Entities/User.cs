using Abstrict.Api.Models.Enums;

namespace Abstrict.Api.Models.Entities;

public sealed class User : Entity
{
    public string? Email { get; set; }
    public required string PhoneNumber { get; set; }
    public string? PasswordHash { get; set; }
    public UserRole Role { get; set; }
    public AccountStatus Status { get; set; } = AccountStatus.Pending;
    public DateTimeOffset? EmailVerifiedAtUtc { get; set; }
    public DateTimeOffset? PhoneVerifiedAtUtc { get; set; }
    public DateTimeOffset? TermsAcceptedAtUtc { get; set; }
    public DateTimeOffset? PrivacyPolicyAcceptedAtUtc { get; set; }
    public CustomerProfile? CustomerProfile { get; set; }
    public ICollection<CustomerAddress> CustomerAddresses { get; set; } = new List<CustomerAddress>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public ICollection<PhoneOtpChallenge> PhoneOtpChallenges { get; set; } = new List<PhoneOtpChallenge>();
    public ICollection<AuthSession> AuthSessions { get; set; } = new List<AuthSession>();
}
