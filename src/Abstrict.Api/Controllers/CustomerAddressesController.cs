using Abstrict.Api.Common;
using Abstrict.Api.DTOs.Requests;
using Abstrict.Api.DTOs.Responses;
using Abstrict.Api.Models.Enums;
using Abstrict.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Abstrict.Api.Controllers;

/// <summary>Sổ địa chỉ của khách hàng, dùng khi đặt lịch.</summary>
[ApiController]
[Authorize(Roles = nameof(UserRole.Customer))]
[Route(ApiRoutes.Version1 + "/customer/addresses")]
public sealed class CustomerAddressesController(ICustomerAddressService addressService) : BookingControllerBase
{
    /// <summary>Danh sách địa chỉ của tôi, địa chỉ mặc định đứng đầu.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CustomerAddressResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CustomerAddressResponse>>> List(CancellationToken cancellationToken) =>
        await Run(async () => await addressService.ListAsync(CurrentUserId, cancellationToken));

    /// <summary>Thêm địa chỉ. Địa chỉ đầu tiên tự động là mặc định.</summary>
    [HttpPost]
    [EnableRateLimiting("booking-write")]
    [ProducesResponseType<CustomerAddressResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CustomerAddressResponse>> Create(SaveCustomerAddressRequest request, CancellationToken cancellationToken) =>
        await Run(async () => await addressService.CreateAsync(CurrentUserId, request, cancellationToken), StatusCodes.Status201Created);

    /// <summary>Sửa địa chỉ.</summary>
    [HttpPut("{addressId:guid}")]
    [EnableRateLimiting("booking-write")]
    [ProducesResponseType<CustomerAddressResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerAddressResponse>> Update(Guid addressId, SaveCustomerAddressRequest request, CancellationToken cancellationToken) =>
        await Run(async () => await addressService.UpdateAsync(CurrentUserId, addressId, request, cancellationToken));

    /// <summary>Xóa địa chỉ. Các đơn đã đặt vẫn giữ bản chụp địa chỉ.</summary>
    [HttpDelete("{addressId:guid}")]
    [EnableRateLimiting("booking-write")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid addressId, CancellationToken cancellationToken) =>
        await RunNoContent(async () => await addressService.DeleteAsync(CurrentUserId, addressId, cancellationToken));
}
