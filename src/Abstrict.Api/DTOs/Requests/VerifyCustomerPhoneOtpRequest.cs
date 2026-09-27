using System.ComponentModel.DataAnnotations;

namespace Abstrict.Api.DTOs.Requests;

public sealed class VerifyCustomerPhoneOtpRequest
{
    [Required]
    public Guid ChallengeId { get; init; }

    [Required, RegularExpression("^[0-9]{6}$", ErrorMessage = "Mã OTP phải gồm đúng 6 chữ số.")]
    public string Code { get; init; } = string.Empty;
}
