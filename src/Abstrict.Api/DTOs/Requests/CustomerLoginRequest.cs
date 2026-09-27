using System.ComponentModel.DataAnnotations;

namespace Abstrict.Api.DTOs.Requests;

public sealed class CustomerLoginRequest
{
    [Required, StringLength(24, MinimumLength = 9)]
    public string PhoneNumber { get; init; } = string.Empty;

    [Required, StringLength(128, MinimumLength = 1)]
    public string Password { get; init; } = string.Empty;
}
