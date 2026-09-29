using System.Security.Claims;
using Abstrict.Api.Common;
using Abstrict.Api.DTOs.Requests;
using Abstrict.Api.DTOs.Responses;
using Abstrict.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abstrict.Api.Controllers;

/// <summary>
/// Duyệt hồ sơ đăng ký freelancer dành cho quản trị viên.
/// </summary>
/// <remarks>
/// Liệt kê, xem chi tiết, xem tài liệu, bắt đầu duyệt và ra quyết định đối với hồ sơ freelancer.
/// Yêu cầu access token với vai trò Admin.
/// </remarks>
[ApiController]
[Authorize(Roles = "Admin")]
[Route(ApiRoutes.Version1 + "/admin/freelancer-applications")]
public sealed class AdminFreelancerApplicationsController(IProviderApprovalService approvalService) : ControllerBase
{
    /// <summary>
    /// Lấy danh sách hồ sơ freelancer (phân trang).
    /// </summary>
    /// <remarks>
    /// Hỗ trợ lọc theo trạng thái qua tham số <c>status</c> và phân trang bằng <c>page</c>/<c>pageSize</c>.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType<PagedResponse<AdminFreelancerApplicationSummaryResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<AdminFreelancerApplicationSummaryResponse>>> List(
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        return Ok(await approvalService.ListAsync(status, page, pageSize, cancellationToken));
    }

    /// <summary>
    /// Lấy chi tiết một hồ sơ freelancer theo id.
    /// </summary>
    /// <remarks>
    /// Trả về toàn bộ thông tin hồ sơ phục vụ duyệt. Trả về 404 nếu không tìm thấy.
    /// </remarks>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<AdminFreelancerApplicationDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminFreelancerApplicationDetailResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        var detail = await approvalService.GetDetailAsync(id, cancellationToken);
        return detail is null ? NotFound() : Ok(detail);
    }

    /// <summary>
    /// Xem nội dung tệp của một tài liệu trong hồ sơ freelancer.
    /// </summary>
    /// <remarks>
    /// Trả về luồng tệp tài liệu để admin kiểm tra. Trả về 404 nếu không tìm thấy. IP người xem được ghi nhận để kiểm toán.
    /// </remarks>
    [HttpGet("{id:guid}/documents/{documentId:guid}/content")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDocumentContent(Guid id, Guid documentId, CancellationToken cancellationToken)
    {
        var result = await approvalService.GetDocumentContentAsync(id, documentId, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        if (result is null)
            return NotFound();
        return File(result.Stream, result.ContentType, result.FileName);
    }

    /// <summary>
    /// Bắt đầu duyệt một hồ sơ freelancer.
    /// </summary>
    /// <remarks>
    /// Chuyển hồ sơ sang trạng thái đang duyệt. Yêu cầu số phiên bản hồ sơ (<c>submissionVersion</c>);
    /// trả về 409 nếu phiên bản không còn hợp lệ.
    /// </remarks>
    [HttpPost("{id:guid}/start-review")]
    [ProducesResponseType<AdminFreelancerApplicationDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AdminFreelancerApplicationDetailResponse>> StartReview(Guid id, StartReviewRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var detail = await approvalService.StartReviewAsync(id, request.SubmissionVersion, CurrentAdminId(), cancellationToken);
            return detail is null ? NotFound() : Ok(detail);
        }
        catch (ApiFlowException exception)
        {
            return this.ToFlowProblem(exception);
        }
    }

    /// <summary>
    /// Ra quyết định duyệt hồ sơ freelancer.
    /// </summary>
    /// <remarks>
    /// Phê duyệt hoặc từ chối hồ sơ kèm lý do và danh sách yêu cầu chỉnh sửa (nếu có).
    /// Yêu cầu số phiên bản hồ sơ; trả về 409 nếu phiên bản không còn hợp lệ.
    /// </remarks>
    [HttpPost("{id:guid}/decision")]
    [ProducesResponseType<AdminFreelancerApplicationDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AdminFreelancerApplicationDetailResponse>> Decide(Guid id, AdminApplicationDecisionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var detail = await approvalService.DecideAsync(id, request, CurrentAdminId(), cancellationToken);
            return detail is null ? NotFound() : Ok(detail);
        }
        catch (ApiFlowException exception)
        {
            return this.ToFlowProblem(exception);
        }
    }

    private Guid CurrentAdminId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? throw new ApiFlowException(StatusCodes.Status401Unauthorized, "UNAUTHENTICATED", "Thiếu thông tin xác thực."));
}
