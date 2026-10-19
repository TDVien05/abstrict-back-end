using System.ComponentModel.DataAnnotations;

namespace Abstrict.Api.DTOs.Requests;

public sealed class CreateProviderRatingRequest
{
    [Range(1, 5)]
    public int Score { get; init; }

    [StringLength(2000)]
    public string? Comment { get; init; }
}
