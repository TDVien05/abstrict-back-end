using System.Security.Claims;
using Abstrict.Api.Common;
using Abstrict.Api.DTOs.Requests;
using Abstrict.Api.DTOs.Responses;
using Abstrict.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Net.Http.Headers;

namespace Abstrict.Api.Controllers;

/// <summary>
/// Onboarding và KYC của freelancer đang đăng nhập.
/// </summary>
/// <remarks>
/// Quản lý hồ sơ onboarding của freelancer hiện tại: thông tin cá nhân, kỹ năng, ngân hàng,
/// tài liệu xác minh, đồng thuận KYC, OCR giấy tờ, so khớp khuôn mặt, và gửi/mở lại hồ sơ.
/// Yêu cầu access token với vai trò Freelancer.
/// </remarks>
[ApiController]
[Authorize(Roles = "Freelancer")]
[Route(ApiRoutes.Version1 + "/freelancers/me/onboarding")]
public sealed class FreelancerOnboardingController(IFreelancerOnboardingService onboardingService) : ControllerBase
{
    /// <summary>
    /// Lấy trạng thái hồ sơ onboarding của freelancer hiện tại.
    /// </summary>
    /// <remarks>
    /// Trả về thông tin cá nhân, nhận dạng, tài liệu, kỹ năng/ngân hàng, các trường còn thiếu
    /// và yêu cầu KYC hiện hành. Trả về 404 nếu chưa có hồ sơ.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType<FreelancerOnboardingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FreelancerOnboardingResponse>> Get(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await onboardingService.GetOnboardingAsync(CurrentUserId(), cancellationToken));
        }
        catch (ApiFlowException exception)
        {
            return this.ToFlowProblem(exception);
        }
    }

    /// <summary>
    /// Cập nhật thông tin hồ sơ onboarding (thông tin cá nhân, kỹ năng, ngân hàng).
    /// </summary>
    /// <remarks>
    /// Cập nhật một phần hồ sơ onboarding. Gửi header <c>If-Match</c> với số phiên bản hiện tại
    /// để kiểm soát đồng thời; trả về 409 nếu phiên bản không khớp.
    /// </remarks>
    [HttpPatch]
    [ProducesResponseType<FreelancerOnboardingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<FreelancerOnboardingResponse>> Patch(UpdateFreelancerOnboardingRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await onboardingService.UpdateOnboardingAsync(CurrentUserId(), request, ExpectedVersion(), cancellationToken));
        }
        catch (ApiFlowException exception)
        {
            return this.ToFlowProblem(exception);
        }
    }

    /// <summary>
    /// Ghi nhận sự đồng thuận KYC của freelancer.
    /// </summary>
    /// <remarks>
    /// Lưu lại việc chấp nhận/không chấp nhận một loại đồng thuận KYC kèm phiên bản nội dung.
    /// Địa chỉ IP của yêu cầu được ghi nhận để kiểm toán. Trả về 204 khi thành công.
    /// </remarks>
    [HttpPost("consents")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RecordConsent(RecordKycConsentRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await onboardingService.RecordConsentAsync(CurrentUserId(), request, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
            return NoContent();
        }
        catch (ApiFlowException exception)
        {
            return this.ToFlowProblem(exception);
        }
    }

    /// <summary>
    /// Tải lên tài liệu xác minh (multipart/form-data).
    /// </summary>
    /// <remarks>
    /// Nhận tệp tài liệu kèm metadata (loại tài liệu, nơi cấp, ngày cấp/hết hạn). Kích thước tối đa 20 MB.
    /// Trả về 201 với thông tin tài liệu đã tải; 415 nếu loại nội dung không được hỗ trợ.
    /// </remarks>
    [HttpPost("documents")]
    [RequestSizeLimit(20_000_000)]
    [ProducesResponseType<FreelancerDocumentResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType)]
    public async Task<ActionResult<FreelancerDocumentResponse>> UploadDocument([FromForm] UploadOnboardingDocumentRequest metadata, IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return this.ToFlowProblem(new ApiFlowException(StatusCodes.Status400BadRequest, "VALIDATION_FAILED", "Thiếu tệp tải lên."));

        try
        {
            await using var stream = file.OpenReadStream();
            var response = await onboardingService.UploadDocumentAsync(
                CurrentUserId(), stream, file.FileName, file.ContentType, metadata, cancellationToken);
            return CreatedAtAction(nameof(Get), new { id = response.DocumentId }, response);
        }
        catch (ApiFlowException exception)
        {
            return this.ToFlowProblem(exception);
        }
    }

    /// <summary>
    /// Tải nội dung tệp của một tài liệu xác minh.
    /// </summary>
    /// <remarks>
    /// Trả về luồng tệp của tài liệu thuộc hồ sơ freelancer hiện tại. Trả về 404 nếu không tìm thấy.
    /// </remarks>
    [HttpGet("documents/{id:guid}/content")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDocumentContent(Guid id, CancellationToken cancellationToken)
    {
        var result = await onboardingService.GetDocumentContentAsync(CurrentUserId(), id, cancellationToken);
        if (result is null)
            return NotFound();
        return File(result.Stream, result.ContentType, result.FileName);
    }

    /// <summary>
    /// Xóa một tài liệu xác minh khỏi hồ sơ.
    /// </summary>
    /// <remarks>
    /// Xóa tài liệu theo id. Gửi header <c>If-Match</c> với số phiên bản hiện tại để kiểm soát đồng thời.
    /// Trả về 204 khi thành công.
    /// </remarks>
    [HttpDelete("documents/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteDocument(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await onboardingService.DeleteDocumentAsync(CurrentUserId(), id, ExpectedVersion(), cancellationToken);
            return NoContent();
        }
        catch (ApiFlowException exception)
        {
            return this.ToFlowProblem(exception);
        }
    }

    /// <summary>
    /// Bắt đầu OCR giấy tờ tùy thân (mặt trước và mặt sau).
    /// </summary>
    /// <remarks>
    /// Tạo thao tác KYC xử lý bất đồng bộ để trích xuất thông tin từ hai tài liệu đã tải lên.
    /// Trả về 202 kèm id thao tác để theo dõi tiến trình. Hỗ trợ header <c>Idempotency-Key</c>.
    /// Giới hạn tần suất theo địa chỉ IP (20 lần/phút).
    /// </remarks>
    [HttpPost("identity/ocr")]
    [EnableRateLimiting("kyc-operation")]
    [ProducesResponseType<KycOperationResponse>(StatusCodes.Status202Accepted)]
    public async Task<ActionResult<KycOperationResponse>> StartOcr(StartIdentityOcrRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await onboardingService.StartOcrAsync(CurrentUserId(), request, IdempotencyKey(), ExpectedVersion(), cancellationToken);
            return Accepted(response);
        }
        catch (ApiFlowException exception)
        {
            return this.ToFlowProblem(exception);
        }
    }

    /// <summary>
    /// Xác nhận thông tin nhận dạng sau khi OCR.
    /// </summary>
    /// <remarks>
    /// Xác nhận (kèm chỉnh sửa nếu có) kết quả OCR của thao tác nhận dạng; cập nhật hồ sơ onboarding
    /// và trả về trạng thái mới nhất.
    /// </remarks>
    [HttpPost("identity/confirm")]
    [ProducesResponseType<FreelancerOnboardingResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<FreelancerOnboardingResponse>> ConfirmIdentity(ConfirmIdentityRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await onboardingService.ConfirmIdentityAsync(CurrentUserId(), request, cancellationToken));
        }
        catch (ApiFlowException exception)
        {
            return this.ToFlowProblem(exception);
        }
    }

    /// <summary>
    /// Bắt đầu so khớp khuôn mặt giữa ảnh selfie và giấy tờ tùy thân.
    /// </summary>
    /// <remarks>
    /// Tạo thao tác KYC xử lý bất đồng bộ để so khớp khuôn mặt. Trả về 202 kèm id thao tác để theo dõi.
    /// Hỗ trợ header <c>Idempotency-Key</c>. Giới hạn tần suất theo địa chỉ IP (20 lần/phút).
    /// </remarks>
    [HttpPost("identity/face-match")]
    [EnableRateLimiting("kyc-operation")]
    [ProducesResponseType<KycOperationResponse>(StatusCodes.Status202Accepted)]
    public async Task<ActionResult<KycOperationResponse>> StartFaceMatch(StartFaceMatchRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await onboardingService.StartFaceMatchAsync(CurrentUserId(), request, IdempotencyKey(), cancellationToken);
            return Accepted(response);
        }
        catch (ApiFlowException exception)
        {
            return this.ToFlowProblem(exception);
        }
    }

    /// <summary>
    /// Lấy trạng thái của một thao tác KYC (OCR / so khớp khuôn mặt).
    /// </summary>
    /// <remarks>
    /// Dùng để polling kết quả thao tác bất đồng bộ theo id. Trả về 404 nếu không tìm thấy.
    /// </remarks>
    [HttpGet("operations/{id:guid}")]
    [ProducesResponseType<KycOperationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<KycOperationResponse>> GetOperation(Guid id, CancellationToken cancellationToken)
    {
        var response = await onboardingService.GetOperationAsync(CurrentUserId(), id, cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    /// <summary>
    /// Gửi hồ sơ onboarding để chờ duyệt.
    /// </summary>
    /// <remarks>
    /// Kiểm tra điều kiện hoàn tất và gửi hồ sơ kèm phiên bản đồng thuận cuối cùng.
    /// Gửi header <c>If-Match</c> với số phiên bản hiện tại. Trả về 202 kèm tóm tắt hồ sơ đã gửi.
    /// </remarks>
    [HttpPost("submit")]
    [ProducesResponseType<FreelancerSubmissionSummaryResponse>(StatusCodes.Status202Accepted)]
    public async Task<ActionResult<FreelancerSubmissionSummaryResponse>> Submit(SubmitApplicationRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await onboardingService.SubmitAsync(CurrentUserId(), request, ExpectedVersion(), cancellationToken);
            return Accepted(response);
        }
        catch (ApiFlowException exception)
        {
            return this.ToFlowProblem(exception);
        }
    }

    /// <summary>
    /// Mở lại hồ sơ sau khi bị yêu cầu chỉnh sửa.
    /// </summary>
    /// <remarks>
    /// Cho phép freelancer chỉnh sửa và gửi lại hồ sơ đã bị từ chối/yêu cầu bổ sung, kèm lý do.
    /// </remarks>
    [HttpPost("reopen")]
    [ProducesResponseType<FreelancerOnboardingResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<FreelancerOnboardingResponse>> Reopen(ReopenApplicationRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await onboardingService.ReopenAsync(CurrentUserId(), request, cancellationToken));
        }
        catch (ApiFlowException exception)
        {
            return this.ToFlowProblem(exception);
        }
    }

    private Guid CurrentUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? throw new ApiFlowException(StatusCodes.Status401Unauthorized, "UNAUTHENTICATED", "Thiếu thông tin xác thực."));

    private int? ExpectedVersion()
    {
        if (!Request.Headers.TryGetValue(HeaderNames.IfMatch, out var values))
            return null;

        var raw = values.ToString().Trim().Trim('"');
        return int.TryParse(raw, out var version) ? version : null;
    }

    private string? IdempotencyKey() =>
        Request.Headers.TryGetValue("Idempotency-Key", out var values) && !string.IsNullOrWhiteSpace(values)
            ? values.ToString()
            : null;
}
