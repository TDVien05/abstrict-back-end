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

/// <summary>
/// Hồ sơ đăng ký của công ty đối tác.
/// </summary>
/// <remarks>
/// Công ty khai thông tin pháp nhân, người đại diện, khu vực nhận ca, tải giấy tờ rồi gửi duyệt.
/// Chỉ sửa được khi hồ sơ ở trạng thái Draft hoặc Rejected.
/// </remarks>
[ApiController]
[Authorize(Roles = nameof(UserRole.Company))]
[Route(ApiRoutes.Version1 + "/company/onboarding")]
public sealed class CompanyOnboardingController(ICompanyOnboardingService onboardingService) : KycControllerBase
{
    /// <summary>Toàn bộ trạng thái hồ sơ và bước tiếp theo cần làm.</summary>
    [HttpGet]
    [ProducesResponseType<CompanyOnboardingResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CompanyOnboardingResponse>> Get(CancellationToken cancellationToken) =>
        await Run(async () => await onboardingService.GetAsync(CurrentUserId, cancellationToken));

    /// <summary>Lưu thông tin pháp nhân. Lần lưu đầu tiên tạo hồ sơ ở trạng thái Draft.</summary>
    [HttpPut("company")]
    [EnableRateLimiting("kyc-write")]
    [ProducesResponseType<CompanyOnboardingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CompanyOnboardingResponse>> SaveCompany(SaveCompanyProfileRequest request, CancellationToken cancellationToken) =>
        await Run(async () => await onboardingService.SaveCompanyAsync(CurrentUserId, request, cancellationToken));

    /// <summary>Lưu người đại diện. Số CCCD được mã hóa khi lưu, response chỉ có 4 số cuối.</summary>
    [HttpPut("representative")]
    [EnableRateLimiting("kyc-write")]
    [ProducesResponseType<CompanyOnboardingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CompanyOnboardingResponse>> SaveRepresentative(SaveCompanyRepresentativeRequest request, CancellationToken cancellationToken) =>
        await Run(async () => await onboardingService.SaveRepresentativeAsync(CurrentUserId, request, cancellationToken));

    /// <summary>Thay toàn bộ danh sách khu vực nhận ca (1–20 khu vực từ <c>/catalog/service-areas</c>).</summary>
    [HttpPut("service-areas")]
    [EnableRateLimiting("kyc-write")]
    [ProducesResponseType<CompanyOnboardingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CompanyOnboardingResponse>> SaveServiceAreas(SaveCompanyServiceAreasRequest request, CancellationToken cancellationToken) =>
        await Run(async () => await onboardingService.SaveServiceAreasAsync(CurrentUserId, request, cancellationToken));

    /// <summary>Tải lên hoặc thay một giấy tờ.</summary>
    /// <remarks>
    /// <c>type</c> là BusinessRegistration, CitizenIdFront, CitizenIdBack hoặc LiabilityInsurance. Gửi <c>multipart/form-data</c>
    /// với trường <c>file</c>, tối đa 10 MB. ĐKKD và bảo hiểm nhận JPG, PNG hoặc PDF; CCCD chỉ nhận JPG hoặc PNG.
    /// </remarks>
    [HttpPut("documents/{type}")]
    [EnableRateLimiting("kyc-write")]
    [RequestSizeLimit(CompanyDocument.MaximumBytes + 256 * 1024)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<CompanyOnboardingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType)]
    public async Task<ActionResult<CompanyOnboardingResponse>> UploadDocument(
        VerificationDocumentType type, IFormFile file, CancellationToken cancellationToken) =>
        await Run(async () =>
        {
            await using var stream = file.OpenReadStream();
            return await onboardingService.UploadDocumentAsync(CurrentUserId, type, file.FileName, file.Length, stream, cancellationToken);
        });

    /// <summary>Nội dung một giấy tờ của chính công ty.</summary>
    [HttpGet("documents/{documentId:guid}/content")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDocumentContent(Guid documentId, CancellationToken cancellationToken)
    {
        try
        {
            var content = await onboardingService.OpenDocumentAsync(CurrentUserId, documentId, cancellationToken);
            return content is null
                ? ToProblem(new KycFlowException(StatusCodes.Status404NotFound, "DOCUMENT_NOT_FOUND", "Không tìm thấy giấy tờ."))
                : ImageFile(content);
        }
        catch (KycFlowException exception)
        {
            return ToProblem(exception);
        }
    }

    /// <summary>Gửi hồ sơ cho admin duyệt. Cần đủ pháp nhân, người đại diện, khu vực và giấy tờ bắt buộc.</summary>
    [HttpPost("submit")]
    [EnableRateLimiting("kyc-write")]
    [ProducesResponseType<CompanyOnboardingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CompanyOnboardingResponse>> Submit(CancellationToken cancellationToken) =>
        await Run(async () => await onboardingService.SubmitAsync(CurrentUserId, cancellationToken));
}
