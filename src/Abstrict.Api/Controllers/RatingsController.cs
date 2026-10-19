using Abstrict.Api.Common;
using Abstrict.Api.DTOs.Requests;
using Abstrict.Api.DTOs.Responses;
using Abstrict.Api.Models.Enums;
using Abstrict.Api.Services.Implementations;
using Abstrict.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Abstrict.Api.Controllers;

[ApiController]
[Authorize(Roles = nameof(UserRole.Customer))]
[Route(ApiRoutes.Version1 + "/bookings")]
public sealed class CustomerRatingsController(IRatingService ratingService) : BookingControllerBase
{
    [HttpPost("{bookingId:guid}/rating")]
    [EnableRateLimiting("booking-write")]
    [ProducesResponseType<RatingResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RatingResponse>> Create(Guid bookingId, CreateProviderRatingRequest request, CancellationToken cancellationToken) =>
        await Run(async () => await ratingService.CreateProviderRatingAsync(CurrentUserId, bookingId, request, cancellationToken), StatusCodes.Status201Created);

    [HttpGet("{bookingId:guid}/rating")]
    [ProducesResponseType<RatingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RatingResponse>> Get(Guid bookingId, CancellationToken cancellationToken) =>
        await Run(async () => await ratingService.GetCustomerRatingAsync(CurrentUserId, bookingId, cancellationToken));
}

[ApiController]
[AllowAnonymous]
[Route(ApiRoutes.Version1 + "/freelancers")]
public sealed class PublicRatingsController(IRatingService ratingService) : BookingControllerBase
{
    [HttpGet("{providerId:guid}/ratings")]
    [ProducesResponseType<PagedResponse<PublicRatingResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResponse<PublicRatingResponse>>> List(
        Guid providerId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) =>
        await Run(async () => await ratingService.ListProviderRatingsAsync(providerId, page, pageSize, cancellationToken));
}
