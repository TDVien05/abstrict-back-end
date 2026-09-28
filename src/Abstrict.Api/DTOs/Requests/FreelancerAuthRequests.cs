using System.ComponentModel.DataAnnotations;

namespace Abstrict.Api.DTOs.Requests;

public sealed class RegisterFreelancerRequest
{
    [Required, StringLength(160, MinimumLength = 2)]
    public string FullName { get; init; } = string.Empty;

    [Required, StringLength(24, MinimumLength = 9)]
    public string PhoneNumber { get; init; } = string.Empty;

    [Required, StringLength(128, MinimumLength = 8)]
    [RegularExpression("^(?=.*\\p{L})(?=.*\\d).+$", ErrorMessage = "Mật khẩu cần có ít nhất một chữ cái và một chữ số.")]
    public string Password { get; init; } = string.Empty;

    [Required, StringLength(128, MinimumLength = 8)]
    public string ConfirmPassword { get; init; } = string.Empty;

    public bool AcceptTermsAndPrivacy { get; init; }
}

public sealed class VerifyFreelancerPhoneOtpRequest
{
    [Required]
    public Guid ChallengeId { get; init; }

    [Required, RegularExpression("^[0-9]{6}$")]
    public string Code { get; init; } = string.Empty;
}

public sealed class ResendFreelancerPhoneOtpRequest
{
    [Required]
    public Guid ChallengeId { get; init; }
}

public sealed class FreelancerLoginRequest
{
    [Required, StringLength(24, MinimumLength = 9)]
    public string PhoneNumber { get; init; } = string.Empty;

    [Required, StringLength(128, MinimumLength = 1)]
    public string Password { get; init; } = string.Empty;
}
