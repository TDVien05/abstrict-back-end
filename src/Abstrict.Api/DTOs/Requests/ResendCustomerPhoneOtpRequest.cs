using System.ComponentModel.DataAnnotations;

namespace Abstrict.Api.DTOs.Requests;

public sealed class ResendCustomerPhoneOtpRequest
{
    [Required]
    public Guid ChallengeId { get; init; }
}
